using System.Text.Json.Serialization;

namespace StoryOptimizer.Models;

public class ChapterProcessingRequest
{
    public string ModelName { get; set; } = "";
    public double Temperature { get; set; } = 0.8;
    public int ContextWindowSize { get; set; } = 4096;
    public List<CharacterInfo> KnownCharacters { get; set; } = new();
    public List<string> PreviousChapterSummaries { get; set; } = new();
    public string ChapterContent { get; set; } = "";
    public string ChapterMarker { get; set; } = "##";
    public string Ruleset { get; set; } = "";
}

public class ChapterProcessingResponse
{
    [JsonPropertyName("revisedChapter")]
    public string RevisedChapter { get; set; } = "";

    [JsonPropertyName("updatedCharacters")]
    public List<CharacterInfo> UpdatedCharacters { get; set; } = new();

    [JsonPropertyName("chapterSummary")]
    public string ChapterSummary { get; set; } = "";
}
