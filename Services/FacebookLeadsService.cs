using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UserRoles.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace UserRoles.Services
{
    public class FacebookLeadsService : IFacebookLeadsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FacebookLeadsService> _logger;
        private readonly IConfiguration _configuration;

        public FacebookLeadsService(HttpClient httpClient, ILogger<FacebookLeadsService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<FacebookLeadDto>> FetchLeadsAsync(string? formId = null)
        {
            try
            {
                string baseUrl = _configuration["FacebookLeads:BaseUrl"] ?? "https://crmsocial.metagensoft.com/api/facebook/leads";
                string apiUrl = string.IsNullOrEmpty(formId) ? baseUrl : $"{baseUrl}?formId={formId}";

                _logger.LogInformation("Fetching leads from Facebook API: {Url}", apiUrl);
                var leads = await _httpClient.GetFromJsonAsync<List<FacebookLeadDto>>(apiUrl);
                
                if (leads != null && !string.IsNullOrEmpty(formId))
                {
                    foreach(var lead in leads) lead.FormId = formId;
                }

                return leads ?? new List<FacebookLeadDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leads from Facebook API.");
                return new List<FacebookLeadDto>();
            }
        }
    }
}
