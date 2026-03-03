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

        [JsonPropertyName("campaignName")]
        public string? CampaignName { get; set; }

        [JsonPropertyName("adsetName")]
        public string? AdsetName { get; set; }

        [JsonPropertyName("adName")]
        public string? AdName { get; set; }

        [JsonPropertyName("metaCreatedAt")]
        public string? MetaCreatedAt { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, string>? Fields { get; set; }
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
