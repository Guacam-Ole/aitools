using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace StoryOptimizer.Services;

public class ComfyUIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _comfyUIBaseUrl;

    public ComfyUIService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _comfyUIBaseUrl = configuration["Services:ComfyUIBaseUrl"] ?? "http://localhost:8188";
        Console.WriteLine($"[ComfyUIService] Using ComfyUI base URL: {_comfyUIBaseUrl}");
    }

    public async Task<string?> GenerateCharacterPortrait(string characterDescription, string workflowPath)
    {
        try
        {
            // Load the workflow JSON (already in API format)
            var workflowJson = await File.ReadAllTextAsync(workflowPath);

            // Replace #POSITIVE# with the character description
            workflowJson = workflowJson.Replace("#POSITIVE#", characterDescription);

            // Parse as JsonObject
            var workflow = JsonNode.Parse(workflowJson)?.AsObject();
            if (workflow == null)
            {
                Console.WriteLine("[ComfyUI] Failed to parse workflow JSON");
                return null;
            }

            var clientId = Guid.NewGuid().ToString();

            // Create the payload
            var payload = new JsonObject
            {
                ["prompt"] = workflow,
                ["client_id"] = clientId
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            // Queue the prompt
            var content = new StringContent(
                payload.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            Console.WriteLine($"[ComfyUI] Sending request to {_comfyUIBaseUrl}/prompt");
            var response = await httpClient.PostAsync($"{_comfyUIBaseUrl}/prompt", content);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ComfyUI] Request failed with status {response.StatusCode}. Response: {responseContent}");
                return null;
            }

            var promptResponse = JsonSerializer.Deserialize<ComfyUIPromptResponse>(responseContent);

            if (promptResponse?.PromptId == null)
            {
                Console.WriteLine("[ComfyUI] No prompt ID received");
                return null;
            }

            Console.WriteLine($"[ComfyUI] Prompt queued with ID: {promptResponse.PromptId}");

            // Poll for completion
            var imageFilename = await WaitForCompletion(promptResponse.PromptId, clientId);

            if (imageFilename != null)
            {
                // Get the image URL
                var imageUrl = $"{_comfyUIBaseUrl}/view?filename={imageFilename}&type=output";
                Console.WriteLine($"[ComfyUI] Image generated: {imageUrl}");

                // Download the image and convert to base64
                var base64Image = await DownloadImageAsBase64(imageUrl);
                return base64Image;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ComfyUI] Error generating image: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> WaitForCompletion(string promptId, string clientId)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var maxAttempts = 120; // 2 minutes with 1 second intervals
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            try
            {
                // Check history for this prompt
                var historyResponse = await httpClient.GetAsync($"{_comfyUIBaseUrl}/history/{promptId}");
                if (historyResponse.IsSuccessStatusCode)
                {
                    var historyJson = await historyResponse.Content.ReadAsStringAsync();
                    var history = JsonSerializer.Deserialize<Dictionary<string, ComfyUIHistoryItem>>(historyJson);

                    if (history != null && history.TryGetValue(promptId, out var historyItem))
                    {
                        // Check if completed
                        if (historyItem.Outputs != null)
                        {
                            // Find the SaveImage node output
                            foreach (var output in historyItem.Outputs)
                            {
                                if (output.Value.Images != null && output.Value.Images.Count > 0)
                                {
                                    var image = output.Value.Images[0];
                                    Console.WriteLine($"[ComfyUI] Found image: {image.Filename}");
                                    return image.Filename;
                                }
                            }
                        }
                    }
                }

                await Task.Delay(1000); // Wait 1 second before next poll
                attempts++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComfyUI] Error polling: {ex.Message}");
                await Task.Delay(1000);
                attempts++;
            }
        }

        Console.WriteLine("[ComfyUI] Timeout waiting for image generation");
        return null;
    }

    private async Task<string?> DownloadImageAsBase64(string imageUrl)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
            var base64String = Convert.ToBase64String(imageBytes);

            // Return as data URL that can be used directly in img src
            var dataUrl = $"data:image/png;base64,{base64String}";

            Console.WriteLine($"[ComfyUI] Image converted to base64 ({imageBytes.Length} bytes)");
            return dataUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ComfyUI] Error downloading image: {ex.Message}");
            return null;
        }
    }
}

public class ComfyUIPromptResponse
{
    [JsonPropertyName("prompt_id")]
    public string? PromptId { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("node_errors")]
    public Dictionary<string, object>? NodeErrors { get; set; }
}

public class ComfyUIHistoryItem
{
    [JsonPropertyName("prompt")]
    public object? Prompt { get; set; }

    [JsonPropertyName("outputs")]
    public Dictionary<string, ComfyUIOutput>? Outputs { get; set; }

    [JsonPropertyName("status")]
    public ComfyUIStatus? Status { get; set; }
}

public class ComfyUIOutput
{
    [JsonPropertyName("images")]
    public List<ComfyUIImage>? Images { get; set; }
}

public class ComfyUIImage
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("subfolder")]
    public string Subfolder { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class ComfyUIStatus
{
    [JsonPropertyName("status_str")]
    public string? StatusStr { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("messages")]
    public List<object>? Messages { get; set; }
}
