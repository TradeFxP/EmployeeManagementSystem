using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.Models.Enums;
using UserRoles.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using System.Globalization;

namespace UserRoles.Services
{
    public class FacebookLeadIngestionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FacebookLeadIngestionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _pollingInterval;
        private string[] _formIds;
        private HubConnection? _externalHubConnection;
        private string? _externalHubUrl;

        public FacebookLeadIngestionService(IServiceScopeFactory scopeFactory, ILogger<FacebookLeadIngestionService> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;

            // Load polling interval (default 2 mins)
            int intervalMins = _configuration.GetValue<int>("FacebookLeads:PollingIntervalMinutes", 2);
            if (intervalMins <= 0) intervalMins = 2;
            _pollingInterval = TimeSpan.FromMinutes(intervalMins);

            // Load Form IDs
            _formIds = _configuration.GetSection("FacebookLeads:FormIds").Get<string[]>() 
                      ?? Array.Empty<string>();

            // External SignalR Configuration (Optional)
            _externalHubUrl = _configuration["FacebookLeads:ExternalHubUrl"];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Facebook Lead Ingestion Service (Persistent) is starting.");

            // 1. Setup External SignalR Connection if URL is provided
            if (!string.IsNullOrEmpty(_externalHubUrl))
            {
                _logger.LogInformation("Attempting to connect to external SignalR hub for leads: {Url}", _externalHubUrl);
                _externalHubConnection = new HubConnectionBuilder()
                    .WithUrl(_externalHubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _externalHubConnection.On<FacebookLeadDto>("ReceiveNewLead", async (lead) =>
                {
                    _logger.LogInformation("Real-time lead received via SignalR: {LeadId}", lead.Id);
                    await HandleIncomingLead(lead, lead.FormId ?? "unspecified", stoppingToken);
                });

                try
                {
                    await _externalHubConnection.StartAsync(stoppingToken);
                    _logger.LogInformation("SignalR connection established. Ingestion is now real-time (no polling).");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to external SignalR hub. Falling back to polling.");
                }
            }

            // 2. Main Loop
            while (!stoppingToken.IsCancellationRequested)
            {
                // Only poll if SignalR is not active or not configured
                if (_externalHubConnection == null || _externalHubConnection.State != HubConnectionState.Connected)
                {
                    try
                    {
                        await IngestLeads(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred during Facebook lead ingestion (polling fallback).");
                    }
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("Facebook Lead Ingestion Service (Persistent) is stopping.");
        }

        private async Task IngestLeads(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var leadsService = scope.ServiceProvider.GetRequiredService<IFacebookLeadsService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TaskHub>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();

            // 1. Find the target column 'To Do' for Digi Leads team
            var column = await dbContext.TeamColumns
                .Where(c => c.TeamName == "Digi Leads")
                .OrderBy(c => c.Order)
                .FirstOrDefaultAsync(c => c.ColumnName.ToLower() == "to do", stoppingToken);

            if (column == null)
            {
                _logger.LogWarning("DIAGNOSTIC: No 'To Do' column found for team 'Digi Leads'. Board might not be initialized properly. Skipping ingestion.");
                return;
            }
            _logger.LogInformation("DIAGNOSTIC: Target column found for ingestion: {ColumnName} (ID: {ColumnId})", column.ColumnName, column.Id);

            // 2. Find a default admin user to assign leads to
            var adminUser = await userManager.FindByEmailAsync("admin@gmail.com");
            if (adminUser == null)
            {
                // Fallback to any admin
                adminUser = (await userManager.GetUsersInRoleAsync("Admin")).FirstOrDefault();
            }

            if (adminUser == null)
            {
                _logger.LogWarning("DIAGNOSTIC: No admin user found to assign leads. Please ensure at least one user has the 'Admin' role. Skipping ingestion.");
                return;
            }
            _logger.LogInformation("DIAGNOSTIC: Leads will be assigned to user: {UserName} (ID: {UserId})", adminUser.UserName, adminUser.Id);

            if (_formIds == null || !_formIds.Any())
            {
                // Refresh Form IDs in case config changed
                _formIds = _configuration.GetSection("FacebookLeads:FormIds").Get<string[]>() ?? Array.Empty<string>();
            }

            if (!_formIds.Any())
            {
                _logger.LogWarning("No Facebook Form IDs configured. Skipping ingestion.");
                return;
            }

            foreach (var formId in _formIds)
            {
                if (stoppingToken.IsCancellationRequested) break;

                _logger.LogInformation("Polling leads for Form ID: {FormId}", formId);
                var leads = await leadsService.FetchLeadsAsync(formId);
                
                if (leads == null)
                {
                    _logger.LogWarning("DIAGNOSTIC: API returned null for Form ID: {FormId}", formId);
                    continue;
                }

                _logger.LogInformation("DIAGNOSTIC: Found {Count} leads from API for Form ID: {FormId}", leads.Count, formId);
                if (!leads.Any()) continue;

                var newLeadsToBroadcast = new List<TaskItem>();

                foreach (var leadDto in leads)
                {
                    string externalId = leadDto.Id.ToString();
                    bool alreadyExists = await dbContext.TaskItems.AnyAsync(t => t.ExternalLeadId == externalId, stoppingToken);

                    if (!alreadyExists)
                    {
                        var task = new TaskItem
                        {
                            ExternalLeadId = externalId,
                            FormId = formId,
                            Title = leadDto.Name ?? $"Lead {externalId}",
                            Description = FormatLeadDescription(leadDto, formId),
                            TeamName = "Digi Leads",
                            ColumnId = column.Id,
                            Priority = Models.Enums.TaskPriority.Medium,
                            Status = Models.Enums.TaskStatus.ToDo,
                            CreatedByUserId = adminUser.Id,
                            CreatedAt = DateTime.UtcNow,
                            AssignedToUserId = adminUser.Id,
                            AssignedByUserId = adminUser.Id,
                            AssignedAt = DateTime.UtcNow,
                            CurrentColumnEntryAt = DateTime.UtcNow
                        };

                        dbContext.TaskItems.Add(task);
                        newLeadsToBroadcast.Add(task);
                    }
                    else
                    {
                        _logger.LogDebug("DIAGNOSTIC: Lead {ExternalId} already exists in database. Skipping.", externalId);
                    }
                }

                if (newLeadsToBroadcast.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Saved {Count} new leads for form {FormId}.", newLeadsToBroadcast.Count, formId);

                    // Broadcast to SignalR
                    try
                    {
                        await hubContext.Clients.Group("Digi Leads").SendAsync("NewLeadsDetected", new
                        {
                            leads = newLeadsToBroadcast.Select(t => new { 
                                id = t.Id, 
                                title = t.Title, 
                                externalLeadId = t.ExternalLeadId,
                                formId = t.FormId
                            }),
                            columnId = column.Id
                        }, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to broadcast NewLeadsDetected for form {FormId}.", formId);
                    }
                }
            }
        }

        private async Task HandleIncomingLead(FacebookLeadDto leadDto, string formId, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TaskHub>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();

            var column = await dbContext.TeamColumns
                .Where(c => c.TeamName == "Digi Leads")
                .OrderBy(c => c.Order)
                .FirstOrDefaultAsync(c => c.ColumnName.ToLower() == "to do", stoppingToken);

            if (column == null) return;

            var adminUser = await userManager.FindByEmailAsync("admin@gmail.com") 
                         ?? (await userManager.GetUsersInRoleAsync("Admin")).FirstOrDefault();

            if (adminUser == null) return;

            string externalId = leadDto.Id.ToString();
            bool alreadyExists = await dbContext.TaskItems.AnyAsync(t => t.ExternalLeadId == externalId, stoppingToken);

            if (!alreadyExists)
            {
                var task = new TaskItem
                {
                    ExternalLeadId = externalId,
                    FormId = formId,
                    Title = leadDto.Name ?? $"Lead {externalId}",
                    Description = FormatLeadDescription(leadDto, formId),
                    TeamName = "Digi Leads",
                    ColumnId = column.Id,
                    Priority = Models.Enums.TaskPriority.Medium,
                    Status = Models.Enums.TaskStatus.ToDo,
                    CreatedByUserId = adminUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    AssignedToUserId = adminUser.Id,
                    AssignedByUserId = adminUser.Id,
                    AssignedAt = DateTime.UtcNow,
                    CurrentColumnEntryAt = DateTime.UtcNow
                };

                dbContext.TaskItems.Add(task);
                await dbContext.SaveChangesAsync(stoppingToken);

                // Broadcast locally
                await hubContext.Clients.Group("Digi Leads").SendAsync("NewLeadsDetected", new
                {
                    leads = new[] { new { id = task.Id, title = task.Title, externalLeadId = task.ExternalLeadId, formId = task.FormId } },
                    columnId = column.Id
                }, stoppingToken);
            }
        }

        private string FormatLeadDescription(FacebookLeadDto lead, string formId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### Facebook Lead Data (Form: {formId})");
            sb.AppendLine("---");

            // 1. Use reflection to get all properties from FacebookLeadDto (excluding Fields dictionary)
            var props = typeof(FacebookLeadDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.Name == "Fields") continue;
                
                var value = prop.GetValue(lead);
                if (value != null && !string.IsNullOrEmpty(value.ToString()))
                {
                    // Clean up property name (e.g. CampaignName -> Campaign Name)
                    string label = System.Text.RegularExpressions.Regex.Replace(prop.Name, "([a-z])([A-Z])", "$1 $2");
                    sb.AppendLine($"**{label}:** {value}");
                }
            }

            // 2. Iterate through the Fields dictionary for dynamic questionnaire data
            if (lead.Fields != null && lead.Fields.Any())
            {
                sb.AppendLine("\n### --- Questionnaire / Form Fields ---");
                foreach (var field in lead.Fields)
                {
                    // Format the key nicely (e.g. what_are_you_looking_to_launch? -> What Are You Looking To Launch?)
                    string key = field.Key.Replace("_", " ");
                    if (key.EndsWith("?")) key = key.Substring(0, key.Length - 1);
                    key = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key);

                    sb.AppendLine($"- **{key}:** {field.Value}");
                }
            }

            sb.AppendLine("\n---");
            sb.AppendLine($"*Synced on {DateTime.Now:dd MMM yyyy, hh:mm tt}*");

            return sb.ToString();
        }
    }
}
