using System.Text.Json.Serialization;

namespace StoryOptimizer.Models;

public class SaveData
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.8;

    [JsonPropertyName("contextWindow")]
    public int ContextWindow { get; set; } = 4096;

    [JsonPropertyName("chapter")]
    public string Chapter { get; set; } = "##";

    [JsonPropertyName("ruleset")]
    public string Ruleset { get; set; } = "";

    [JsonPropertyName("before")]
    public string Before { get; set; } = "";

    [JsonPropertyName("characters")]
    public List<CharacterInfo> Characters { get; set; } = new();

    [JsonPropertyName("summaries")]
    public List<string> Summaries { get; set; } = new();
}
