using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UserRoles.Constants;
using UserRoles.Models;
using Microsoft.Extensions.Logging;

namespace UserRoles.Services
{
    public class FacebookLeadsService : IFacebookLeadsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FacebookLeadsService> _logger;

        public FacebookLeadsService(HttpClient httpClient, ILogger<FacebookLeadsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<FacebookLeadDto>> FetchLeadsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching leads from Facebook API: {Url}", ApiConstants.FacebookLeadsApiUrl);
                var leads = await _httpClient.GetFromJsonAsync<List<FacebookLeadDto>>(ApiConstants.FacebookLeadsApiUrl);
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
