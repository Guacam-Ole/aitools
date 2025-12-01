using System.Text.Json.Serialization;

namespace StoryOptimizer.Models;

public class OllamaModelsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel> Models { get; set; } = new();
}

public class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("response")]
    public string Response { get; set; } = "";

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("context")]
    public int[]? Context { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }
}

public class OllamaModelInfoResponse
{
    [JsonPropertyName("modelfile")]
    public string Modelfile { get; set; } = "";

    [JsonPropertyName("parameters")]
    public string Parameters { get; set; } = "";

    [JsonPropertyName("template")]
    public string Template { get; set; } = "";

    [JsonPropertyName("model_info")]
    public Dictionary<string, object>? ModelInfo { get; set; }

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
}

public class OllamaModelDetails
{
    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; } = "";

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; } = "";
}
