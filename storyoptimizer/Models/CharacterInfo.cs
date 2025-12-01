using System.Text.Json.Serialization;

namespace StoryOptimizer.Models;

public class CharacterInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("introducedInChapter")]
    public int IntroducedInChapter { get; set; } = 0;

    [JsonPropertyName("lastChangedInChapter")]
    public int? LastChangedInChapter { get; set; } = null;

    [JsonPropertyName("portraitUrl")]
    public string? PortraitUrl { get; set; } = null;
}

public class CharacterListWrapper
{
    [JsonPropertyName("characters")]
    public List<CharacterInfo> Characters { get; set; } = new();
}
