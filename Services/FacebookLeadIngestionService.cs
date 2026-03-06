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
        private readonly HashSet<string> _seenLeadIds = new HashSet<string>();

        public FacebookLeadIngestionService(IServiceScopeFactory scopeFactory, ILogger<FacebookLeadIngestionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Facebook Lead Ingestion Service (Virtual) is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await IngestLeadsVirtual(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during virtual Facebook lead ingestion.");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }

            _logger.LogInformation("Facebook Lead Ingestion Service (Virtual) is stopping.");
        }

        private async Task IngestLeadsVirtual(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var leadsService = scope.ServiceProvider.GetRequiredService<IFacebookLeadsService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TaskHub>>();

            var leads = await leadsService.FetchLeadsAsync();
            if (leads == null || !leads.Any()) return;

            // We still need the ColumnId to tell the frontend where to put these leads
            // Find Digi Leads team column 'To Do'
            var column = await dbContext.TeamColumns
                .Where(c => c.TeamName == "Digi Leads")
                .OrderBy(c => c.Order)
                .FirstOrDefaultAsync(c => c.ColumnName.ToLower() == "to do", stoppingToken);

            if (column == null)
            {
                _logger.LogWarning("No 'To Do' column found for team 'Digi Leads'. Cannot broadcast virtual leads.");
                return;
            }

            var newLeads = leads.Where(l => !_seenLeadIds.Contains(l.Id.ToString())).ToList();

            if (!newLeads.Any()) return;

            _logger.LogInformation("Detected {Count} new leads. Broadcasting via SignalR.", newLeads.Count);

            foreach (var lead in newLeads)
            {
                _seenLeadIds.Add(lead.Id.ToString());
            }

            // Broadcast to the "Digi Leads" group
            try
            {
                // We send the list of new leads and the target columnId
                await hubContext.Clients.Group("Digi Leads").SendAsync("NewLeadsDetected", new
                {
                    leads = newLeads,
                    columnId = column.Id
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast NewLeadsDetected.");
            }
        }
    }
}
