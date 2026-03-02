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

namespace UserRoles.Services
{
    public class FacebookLeadIngestionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FacebookLeadIngestionService> _logger;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(1);

        public FacebookLeadIngestionService(IServiceScopeFactory scopeFactory, ILogger<FacebookLeadIngestionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Facebook Lead Ingestion Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await IngestLeads(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Facebook lead ingestion.");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("Facebook Lead Ingestion Service is stopping.");
        }

        private async Task IngestLeads(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var leadsService = scope.ServiceProvider.GetRequiredService<IFacebookLeadsService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TaskHub>>();

            var leads = await leadsService.FetchLeadsAsync();
            if (!leads.Any()) return;

            // Find Digi Leads team
            var team = await dbContext.Teams
                .FirstOrDefaultAsync(t => t.Name == "Digi Leads", stoppingToken);

            if (team == null)
            {
                _logger.LogWarning("'Digi Leads' team not found. Skipping ingestion.");
                return;
            }

            // Find 'To Do' column or the first column
            var column = await dbContext.TeamColumns
                .Where(c => c.TeamName == "Digi Leads")
                .OrderBy(c => c.Order)
                .FirstOrDefaultAsync(c => c.ColumnName.ToLower() == "to do", stoppingToken)
                ?? await dbContext.TeamColumns
                    .Where(c => c.TeamName == "Digi Leads")
                    .OrderBy(c => c.Order)
                    .FirstOrDefaultAsync(stoppingToken);

            if (column == null)
            {
                _logger.LogWarning("No columns found for team 'Digi Leads'. Skipping ingestion.");
                return;
            }

            _logger.LogInformation("Found target column: {ColumnName} (Id: {ColumnId}) for team 'Digi Leads'", column.ColumnName, column.Id);

            // Find Admin user
            var adminUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == "admin@gmail.com", stoppingToken);

            if (adminUser == null)
            {
                _logger.LogWarning("Admin user 'admin@gmail.com' not found. Skipping ingestion.");
                return;
            }

            foreach (var lead in leads)
            {
                if (stoppingToken.IsCancellationRequested) break;

                string leadIdStr = lead.Id.ToString();
                _logger.LogInformation("Processing lead: {LeadId} (Name: {Name})", leadIdStr, lead.Name);

                // Deduplication
                var exists = await dbContext.TaskItems
                    .AnyAsync(t => t.ExternalLeadId == leadIdStr, stoppingToken);

                if (exists)
                {
                    _logger.LogInformation("Lead {LeadId} already exists. Skipping.", leadIdStr);
                    continue;
                }

                // Map Lead to TaskItem
                var task = new TaskItem
                {
                    Title = $"New Lead: {lead.Name}",
                    Description = $"Email: {lead.Email}\nPhone: {lead.Phone}\nForm ID: {lead.FormId}",
                    CreatedAt = DateTime.UtcNow,
                    Status = UserRoles.Models.Enums.TaskStatus.ToDo,
                    ColumnId = column.Id,
                    TeamName = team.Name,
                    Priority = TaskPriority.Medium,
                    ExternalLeadId = leadIdStr,
                    CreatedByUserId = adminUser.Id,
                    AssignedToUserId = adminUser.Id
                };

                dbContext.TaskItems.Add(task);
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Ingested new lead: {LeadId} for {Name}", lead.Id, lead.Name);

                // Broadcast via SignalR
                try
                {
                    await hubContext.Clients.Group(team.Name).SendAsync("TaskAdded", new
                    {
                        taskId = task.Id,
                        columnId = column.Id,
                        teamName = team.Name
                    }, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast TaskAdded for lead {LeadId}", lead.Id);
                }
            }
        }
    }
}
