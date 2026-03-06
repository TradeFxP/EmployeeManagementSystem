using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UserRoles.Models
{
    public class FacebookLeadDto
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(LeadIdConverter))]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("formId")]
        public string FormId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("pageId")]
        public string? PageId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("remark")]
        public string? Remark { get; set; }

        [JsonPropertyName("departmentId")]
        public string? DepartmentId { get; set; }

        [JsonPropertyName("departmentName")]
        public string? DepartmentName { get; set; }

        [JsonPropertyName("campaignId")]
        public string? CampaignId { get; set; }

        [JsonPropertyName("campaignName")]
        public string? CampaignName { get; set; }

        [JsonPropertyName("adsetId")]
        public string? AdsetId { get; set; }

        [JsonPropertyName("adsetName")]
        public string? AdsetName { get; set; }

        [JsonPropertyName("adId")]
        public string? AdId { get; set; }

        [JsonPropertyName("adName")]
        public string? AdName { get; set; }

        [JsonPropertyName("metaCreatedAt")]
        public string? MetaCreatedAt { get; set; }

        [JsonPropertyName("syncedAt")]
        public string? SyncedAt { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, object>? Fields { get; set; }
    }

    public class LeadIdConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                return doc.RootElement.GetRawText();
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? "";
            }
            return "";
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
