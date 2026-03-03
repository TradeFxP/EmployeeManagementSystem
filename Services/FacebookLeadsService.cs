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

        public async Task<List<FacebookLeadDto>> FetchLeadsAsync()
        {
            try
            {
                string apiUrl = _configuration["FacebookLeads:ApiUrl"] ?? "";
                _logger.LogInformation("Fetching leads from Facebook API: {Url}", apiUrl);
                var leads = await _httpClient.GetFromJsonAsync<List<FacebookLeadDto>>(apiUrl);
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
