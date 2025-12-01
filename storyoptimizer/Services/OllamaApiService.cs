using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StoryOptimizer.Models;

namespace StoryOptimizer.Services;

public class OllamaApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const string OllamaBaseUrl = "http://mediacenter:11434";

    public OllamaApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetFromJsonAsync<OllamaModelsResponse>($"{OllamaBaseUrl}/api/tags");

            return response?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading models from Ollama: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<ModelInfo> GetModelInfoAsync(string modelName)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var requestBody = new { name = modelName };
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{OllamaBaseUrl}/api/show", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var ollamaModelInfo = JsonSerializer.Deserialize<OllamaModelInfoResponse>(responseJson);

            var modelInfo = new ModelInfo
            {
                MaxContext = 0,  // Start at 0 to make detection failures obvious
                ModelSizeInBillions = 7.0,
                QuantizationBits = 4.5
            };

            // Parse context length from parameters field (e.g., "num_ctx 8192")
            if (!string.IsNullOrEmpty(ollamaModelInfo?.Parameters))
            {
                var numCtxMatch = Regex.Match(ollamaModelInfo.Parameters, @"num_ctx\s+(\d+)", RegexOptions.IgnoreCase);
                if (numCtxMatch.Success && int.TryParse(numCtxMatch.Groups[1].Value, out int ctxSize))
                {
                    modelInfo.MaxContext = ctxSize;
                    Console.WriteLine($"Found context length: {modelInfo.MaxContext} from parameters (num_ctx)");
                }
            }

            // Fallback: Parse context length from model_info
            if (modelInfo.MaxContext == 0 && ollamaModelInfo?.ModelInfo != null)
            {
                // Check for any key containing "context_length" (handles architecture-specific keys like "gemma2.context_length", "llama.context_length", etc.)
                foreach (var kvp in ollamaModelInfo.ModelInfo)
                {
                    if (kvp.Key.Contains("context_length", StringComparison.OrdinalIgnoreCase))
                    {
                        if (kvp.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
                        {
                            modelInfo.MaxContext = jsonElement.GetInt32();
                            Console.WriteLine($"Found context length: {modelInfo.MaxContext} from model_info key '{kvp.Key}'");
                            break;
                        }
                    }
                }

                // If still not found, try max_position_embeddings as last resort
                if (modelInfo.MaxContext == 0 && ollamaModelInfo.ModelInfo.TryGetValue("max_position_embeddings", out var embedValue))
                {
                    if (embedValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        modelInfo.MaxContext = jsonElement.GetInt32();
                        Console.WriteLine($"Found context length: {modelInfo.MaxContext} from model_info key 'max_position_embeddings'");
                    }
                }
            }

            // Parse model size from details.parameter_size (e.g., "13.0B", "7B")
            if (ollamaModelInfo?.Details?.ParameterSize != null)
            {
                var paramSizeMatch = Regex.Match(ollamaModelInfo.Details.ParameterSize, @"(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                if (paramSizeMatch.Success && double.TryParse(paramSizeMatch.Groups[1].Value, out double paramSize))
                {
                    modelInfo.ModelSizeInBillions = paramSize;
                    Console.WriteLine($"Found model size: {modelInfo.ModelSizeInBillions}B from details");
                }
            }
            else
            {
                // Fallback: Parse model size from name (e.g., "llama3:8b", "qwen2.5:14b")
                var nameMatch = Regex.Match(modelName, @"(\d+(?:\.\d+)?)[bB]", RegexOptions.IgnoreCase);
                if (nameMatch.Success && double.TryParse(nameMatch.Groups[1].Value, out double nameSize))
                {
                    modelInfo.ModelSizeInBillions = nameSize;
                    Console.WriteLine($"Found model size: {modelInfo.ModelSizeInBillions}B from name");
                }
            }

            // Parse quantization from details.quantization_level (e.g., "Q5_K_M", "Q4_0")
            if (ollamaModelInfo?.Details?.QuantizationLevel != null)
            {
                var quant = ollamaModelInfo.Details.QuantizationLevel.ToUpper();
                if (quant.Contains("Q2"))
                    modelInfo.QuantizationBits = 2.5;
                else if (quant.Contains("Q3"))
                    modelInfo.QuantizationBits = 3.5;
                else if (quant.Contains("Q4"))
                    modelInfo.QuantizationBits = 4.5;
                else if (quant.Contains("Q5"))
                    modelInfo.QuantizationBits = 5.5;
                else if (quant.Contains("Q6"))
                    modelInfo.QuantizationBits = 6.5;
                else if (quant.Contains("Q8"))
                    modelInfo.QuantizationBits = 8.5;
                else if (quant.Contains("F16") || quant.Contains("FP16"))
                    modelInfo.QuantizationBits = 16.0;

                Console.WriteLine($"Found quantization: {modelInfo.QuantizationBits} bits from '{quant}'");
            }
            else
            {
                // Fallback: Parse quantization from name
                if (modelName.Contains("q2", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 2.5;
                else if (modelName.Contains("q3", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 3.5;
                else if (modelName.Contains("q4", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 4.5;
                else if (modelName.Contains("q5", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 5.5;
                else if (modelName.Contains("q6", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 6.5;
                else if (modelName.Contains("q8", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 8.5;
                else if (modelName.Contains("f16", StringComparison.OrdinalIgnoreCase) || modelName.Contains("fp16", StringComparison.OrdinalIgnoreCase))
                    modelInfo.QuantizationBits = 16.0;

                Console.WriteLine($"Found quantization: {modelInfo.QuantizationBits} bits from name");
            }

            return modelInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting model info from Ollama: {ex.Message}");
            return new ModelInfo { MaxContext = 4096, ModelSizeInBillions = 7.0, QuantizationBits = 4.5 };
        }
    }

    public static double CalculateVramUsageGB(ModelInfo modelInfo, int contextWindowSize)
    {
        // Model weights: (model_size_in_billions × quantization_bits) / 8
        // Example: 12B model with Q5_K_M (5.5 bits) = 12 × 5.5 / 8 = 8.25 GB
        double modelWeightsGB = (modelInfo.ModelSizeInBillions * modelInfo.QuantizationBits) / 8.0;

        // KV cache: context_size × model_size × constant
        // Based on empirical data: 12B Q5_K_M with 16K context uses ~16GB total (8.25GB weights + 7.75GB KV cache)
        // 7.75 = 16384 × 12 × constant → constant ≈ 0.00004
        double kvCacheGB = contextWindowSize * modelInfo.ModelSizeInBillions * 0.00004;

        // Total VRAM usage (no additional overhead multiplier needed, it's already in the empirical constant)
        double totalGB = modelWeightsGB + kvCacheGB;

        Console.WriteLine($"[VRAM Calc] Model: {modelInfo.ModelSizeInBillions}B, Quant: {modelInfo.QuantizationBits} bits, Context: {contextWindowSize}");
        Console.WriteLine($"[VRAM Calc] Weights: {modelWeightsGB:F2} GB, KV Cache: {kvCacheGB:F2} GB, Total: {totalGB:F2} GB");

        return Math.Round(totalGB, 1);
    }

    public async Task UnloadModelAsync(string modelName)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            // Send a request with keep_alive: 0 to unload the model immediately
            var unloadRequest = new
            {
                model = modelName,
                prompt = "",
                keep_alive = 0  // This tells Ollama to unload the model immediately
            };

            var jsonContent = JsonSerializer.Serialize(unloadRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{OllamaBaseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            Console.WriteLine($"[UnloadModel] Model '{modelName}' unloaded successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UnloadModel] Error unloading model: {ex.Message}");
        }
    }

    public async Task<string> CallOllamaAsync(string model, string systemPrompt, string userPrompt, double temperature, int contextWindowSize = 4096, bool jsonFormat = false)
    {
        Console.WriteLine($"[CallOllama] Starting request - Model: {model}, Temp: {temperature}, Context: {contextWindowSize}, JSON: {jsonFormat}");

        var fullResponse = new StringBuilder();
        bool isDone = false;
        int continuationCount = 0;
        const int maxContinuations = 10; // Safety limit

        while (!isDone && continuationCount < maxContinuations)
        {
            if (continuationCount > 0)
            {
                Console.WriteLine($"[REQUEST TYPE] Continuation #{continuationCount}");
            }

            int maxRetries = 15;
            int trycount = 1;
            string partialResponse = string.Empty;
            bool done = true;
            while (trycount < maxRetries)
            {
                (partialResponse, done) = await CallOllamaSingleAsync(model, systemPrompt, userPrompt, temperature,
                    contextWindowSize, jsonFormat);
                if (!string.IsNullOrWhiteSpace(partialResponse)) break;
                trycount++;
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            if (string.IsNullOrWhiteSpace(partialResponse))
            {
                Console.WriteLine("oh-oh!");
            }

            Console.WriteLine($"[CallOllama] Received response - Length: {partialResponse.Length} chars, Done flag: {done}");

            fullResponse.Append(partialResponse);
            isDone = done;

            if (!isDone)
            {
                continuationCount++;
                Console.WriteLine($"[CallOllama] Response truncated. Preparing continuation {continuationCount}/{maxContinuations}...");

                // Prepare simple continuation prompt - just ask to continue, don't resend everything
                // The context from the previous response will be maintained by Ollama
                if (jsonFormat)
                {
                    systemPrompt = "You are continuing a JSON response. Continue exactly where you left off.";
                    userPrompt = "Please continue completing the JSON. Do not restart.";
                }
                else
                {
                    systemPrompt = "You are continuing your previous response. Continue exactly where you left off.";
                    userPrompt = "Please continue.";
                }
            }
        }

        if (!isDone)
        {
            Console.WriteLine($"[CallOllama] WARNING: Response may still be incomplete after {maxContinuations} continuations");
        }
        else
        {
            Console.WriteLine($"[CallOllama] Request completed successfully. Total length: {fullResponse.Length} chars");
        }

        return FilterThinkingTags(fullResponse.ToString());
    }

    private async Task<(string response, bool done)> CallOllamaSingleAsync(string model, string systemPrompt, string userPrompt, double temperature, int contextWindowSize = 4096, bool jsonFormat = false)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromMinutes(30);  // Increased to 30 minutes for long requests

        object ollamaRequest;

        if (jsonFormat)
        {
            ollamaRequest = new
            {
                model,
                prompt = $"System: {systemPrompt}\n\nUser: {userPrompt}",
                stream = true,  // Enable streaming to get proper done detection
                format = "json",
                keep_alive = "5m",  // Keep model loaded for 5 minutes for continuations, then unload
                options = new
                {
                    temperature,
                    top_p = 0.9,           // Nucleus sampling
                    top_k = 40,            // Top-k sampling
                    repeat_penalty = 1.1,  // Penalize repetitions
                    num_predict = -1,      // Max tokens (-1 = unlimited)
                    num_ctx = contextWindowSize  // Context window size
                }
            };
        }
        else
        {
            ollamaRequest = new
            {
                model,
                prompt = $"System: {systemPrompt}\n\nUser: {userPrompt}",
                stream = true,  // Enable streaming to get proper done detection
                keep_alive = "5m",  // Keep model loaded for 5 minutes for continuations, then unload
                options = new
                {
                    temperature,
                    top_p = 0.9,           // Nucleus sampling
                    top_k = 40,            // Top-k sampling
                    repeat_penalty = 1.1,  // Penalize repetitions
                    num_predict = -1,      // Max tokens (-1 = unlimited)
                    num_ctx = contextWindowSize  // Context window size
                }
            };
        }

        var jsonContent = JsonSerializer.Serialize(ollamaRequest);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{OllamaBaseUrl}/api/generate", content);
        response.EnsureSuccessStatusCode();

        // In streaming mode, Ollama sends multiple JSON objects separated by newlines
        // We need to parse each line and accumulate the response
        var responseText = await response.Content.ReadAsStringAsync();
        var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var fullResponse = new StringBuilder();
        bool isDone = false;
        OllamaGenerateResponse? lastResponse = null;

        foreach (var line in lines)
        {
            try
            {
                var chunk = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                if (chunk != null)
                {
                    fullResponse.Append(chunk.Response);
                    isDone = chunk.Done;
                    lastResponse = chunk;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[CallOllamaSingle] Error parsing streaming chunk: {ex.Message}");
            }
        }

        var rawResponse = fullResponse.ToString();
        if (string.IsNullOrEmpty(rawResponse))
        {
            return (string.Empty, true);
        }

        // Log detailed response info for debugging
        Console.WriteLine($"[CallOllamaSingle] Done: {isDone}, DoneReason: {lastResponse?.DoneReason ?? "null"}, EvalCount: {lastResponse?.EvalCount ?? 0}, PromptEvalCount: {lastResponse?.PromptEvalCount ?? 0}");

        return (rawResponse, isDone);
    }

    private string FilterThinkingTags(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove <think>...</think> and <thinking>...</thinking> tags and their content
        // Case insensitive, handles multiline content
        text = Regex.Replace(text, @"<think>.*?</think>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<thinking>.*?</thinking>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Clean up any extra whitespace left behind
        text = Regex.Replace(text, @"\n{3,}", "\n\n"); // Replace 3+ newlines with 2
        text = text.Trim();

        return text;
    }
}
