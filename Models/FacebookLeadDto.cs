using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UserRoles.Models
{
    public class FacebookLeadDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("formId")]
        public string FormId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public Dictionary<string, string>? Fields { get; set; }
    }
}
