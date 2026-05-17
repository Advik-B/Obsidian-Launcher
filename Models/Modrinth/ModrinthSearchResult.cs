using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models.Modrinth;

public class ModrinthSearchResult
{
    [JsonPropertyName("hits")]
    public List<ModrinthProject> Hits { get; set; } = new();

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("total_hits")]
    public int TotalHits { get; set; }
}
