using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models
{
    public class ForgePromotions
    {
        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        [JsonPropertyName("promos")]
        public Dictionary<string, string>? Promos { get; set; }
    }
}