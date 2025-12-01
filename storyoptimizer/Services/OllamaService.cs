using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StoryOptimizer.Services;

public class OllamaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const string OllamaBaseUrl = "http://mediacenter:11434";

    public OllamaService(IHttpClientFactory httpClientFactory)
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

    private async Task UnloadModelAsync(string modelName)
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

    private int GetMaxChapterLength(int contextWindowSize)
    {
        // Return maximum recommended chapter length based on context window size
        return contextWindowSize switch
        {
            <= 4096 => 5000,
            <= 8192 => 15000,
            <= 16384 => 40000,
            _ => 100000  // 32768 or above
        };
    }

    public async Task<List<CharacterInfo>> ExtractCharactersAsync(
        string modelName,
        double temperature,
        int contextWindowSize,
        List<string> chapterContents,
        string chapterMarker,
        Action<string, List<CharacterInfo>>? onProgressUpdate = null)
    {
        try
        {
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine("EXTRACTING CHARACTERS FROM CHAPTERS");
            Console.WriteLine($"Total chapters: {chapterContents.Count}");
            Console.WriteLine($"{new string('=', 60)}\n");

            // Validate chapter lengths against context window size
            var maxChapterLength = GetMaxChapterLength(contextWindowSize);
            for (int i = 0; i < chapterContents.Count; i++)
            {
                if (chapterContents[i] == null)
                {
                    Console.WriteLine($"⚠️  WARNING: Chapter {i + 1} is null, skipping validation");
                    continue;
                }

                var chapterLength = chapterContents[i].Length;
                if (chapterLength > maxChapterLength)
                {
                    var chapterNum = i + 1;
                    Console.WriteLine($"⚠️  WARNING: Chapter {chapterNum} has {chapterLength} characters, exceeds maximum of {maxChapterLength} for context {contextWindowSize}");
                    Console.WriteLine("   This may cause truncation or processing issues. Consider splitting this chapter or increasing context size.");
                }
            }

            var allCharacters = new List<CharacterInfo>();

            for (int i = 0; i < chapterContents.Count; i++)
            {
                // Notify UI before starting
                var statusMsg = $"Extracting characters from chapter {i + 1}/{chapterContents.Count}";
                Console.WriteLine($"[ExtractCharacters] {statusMsg}");
                onProgressUpdate?.Invoke(statusMsg, new List<CharacterInfo>(allCharacters));

                // Extract characters from raw chapter
                var request = new ChapterProcessingRequest
                {
                    ModelName = modelName,
                    Temperature = temperature,
                    ContextWindowSize = contextWindowSize,
                    KnownCharacters = allCharacters,
                    ChapterContent = chapterContents[i],
                    ChapterMarker = chapterMarker,
                    Ruleset = ""
                };

                var extractedCharacters = await ExtractCharactersFromRawAsync(chapterContents[i], request);

                // Merge new characters into cumulative list and track which chapter they were introduced in
                foreach (var extractedChar in extractedCharacters)
                {
                    // Skip characters with null or empty names (invalid data from LLM)
                    if (string.IsNullOrWhiteSpace(extractedChar.Name))
                    {
                        Console.WriteLine($"[ExtractCharacters] Skipping invalid character with empty/null Name");
                        continue;
                    }

                    // Check if this character already exists (by name and role)
                    var existingChar = allCharacters.FirstOrDefault(c =>
                        !string.IsNullOrWhiteSpace(c.Name) &&
                        c.Name.Equals(extractedChar.Name, StringComparison.OrdinalIgnoreCase) &&
                        (c.Role ?? "").Equals(extractedChar.Role ?? "", StringComparison.OrdinalIgnoreCase));

                    if (existingChar != null)
                    {
                        // Check if role or description changed (handle nulls)
                        bool roleChanged = !(existingChar.Role ?? "").Equals(extractedChar.Role ?? "", StringComparison.OrdinalIgnoreCase);
                        bool descriptionChanged = !(existingChar.Description ?? "").Equals(extractedChar.Description ?? "", StringComparison.Ordinal);

                        if (roleChanged || descriptionChanged)
                        {
                            // Update changed fields and track the chapter
                            if (roleChanged)
                            {
                                existingChar.Role = extractedChar.Role;
                            }
                            if (descriptionChanged)
                            {
                                existingChar.Description = extractedChar.Description;
                            }
                            existingChar.LastChangedInChapter = i + 1;  // Chapter numbers are 1-based
                            Console.WriteLine($"  Character '{existingChar.Name}' updated in chapter {i + 1}");
                        }
                        // Keep existingChar.IntroducedInChapter as is
                    }
                    else
                    {
                        // New character - set the introduction chapter, no "last changed" yet
                        extractedChar.IntroducedInChapter = i + 1;  // Chapter numbers are 1-based
                        extractedChar.LastChangedInChapter = null;  // Just introduced, not changed yet
                        allCharacters.Add(extractedChar);
                        Console.WriteLine($"  New character '{extractedChar.Name}' introduced in chapter {i + 1}");
                    }
                }

                Console.WriteLine($"[ExtractCharacters] Chapter {i + 1} complete. Total characters known: {allCharacters.Count}");

                // Unload model to ensure clean context for next chapter extraction
                await UnloadModelAsync(modelName);
            }

            Console.WriteLine($"\n[ExtractCharacters] Complete! Total unique characters: {allCharacters.Count}\n");

            return allCharacters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExtractCharacters] Error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<string>> GenerateSummariesAsync(
        string modelName,
        double temperature,
        int contextWindowSize,
        List<string> chapterContents,
        string chapterMarker,
        Action<string, List<string>>? onProgressUpdate = null)
    {
        try
        {
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine("GENERATING CHAPTER SUMMARIES");
            Console.WriteLine($"Total chapters: {chapterContents.Count}");
            Console.WriteLine($"{new string('=', 60)}\n");

            // Validate chapter lengths against context window size
            var maxChapterLength = GetMaxChapterLength(contextWindowSize);
            for (int i = 0; i < chapterContents.Count; i++)
            {
                if (chapterContents[i] == null)
                {
                    Console.WriteLine($"⚠️  WARNING: Chapter {i + 1} is null, skipping validation");
                    continue;
                }

                var chapterLength = chapterContents[i].Length;
                if (chapterLength > maxChapterLength)
                {
                    var chapterNum = i + 1;
                    Console.WriteLine($"⚠️  WARNING: Chapter {chapterNum} has {chapterLength} characters, exceeds maximum of {maxChapterLength} for context {contextWindowSize}");
                    Console.WriteLine("   This may cause truncation or processing issues. Consider splitting this chapter or increasing context size.");
                }
            }

            var allSummaries = new List<string>();

            for (int i = 0; i < chapterContents.Count; i++)
            {
                // Notify UI before starting
                var statusMsg = $"Generating summary for chapter {i + 1}/{chapterContents.Count}";
                Console.WriteLine($"[GenerateSummaries] {statusMsg}");
                onProgressUpdate?.Invoke(statusMsg, new List<string>(allSummaries));

                var request = new ChapterProcessingRequest
                {
                    ModelName = modelName,
                    Temperature = temperature,
                    ContextWindowSize = contextWindowSize,
                    ChapterContent = chapterContents[i],
                    ChapterMarker = chapterMarker,
                    Ruleset = ""
                };

                var summary = await SummarizeRawChapterAsync(chapterContents[i], request);
                allSummaries.Add(summary);

                Console.WriteLine($"[GenerateSummaries] Chapter {i + 1} summary: {summary.Substring(0, Math.Min(100, summary.Length))}...");

                // Unload model to ensure clean context for next chapter summary
                await UnloadModelAsync(modelName);
            }

            Console.WriteLine($"\n[GenerateSummaries] Complete! Total summaries: {allSummaries.Count}\n");

            return allSummaries;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenerateSummaries] Error: {ex.Message}");
            throw;
        }
    }


    public async Task<List<string>> ReviseChaptersAsync(
        string modelName,
        double temperature,
        int contextWindowSize,
        List<string> chapterContents,
        string ruleset,
        List<CharacterInfo> allCharacters,
        List<string> allSummaries,
        Action<string, int, int>? onChapterRevised = null)
    {
        var revisedChapters = new List<string>();

        try
        {
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine("REVISING CHAPTERS USING PREPARED DATA");
            Console.WriteLine($"Total chapters: {chapterContents.Count}");
            Console.WriteLine($"{new string('=', 60)}\n");

            // Validate chapter lengths against context window size
            var maxChapterLength = GetMaxChapterLength(contextWindowSize);
            for (int i = 0; i < chapterContents.Count; i++)
            {
                if (chapterContents[i] == null)
                {
                    Console.WriteLine($"⚠️  WARNING: Chapter {i + 1} is null, skipping validation");
                    continue;
                }

                var chapterLength = chapterContents[i].Length;
                if (chapterLength > maxChapterLength)
                {
                    var chapterNum = i + 1;
                    Console.WriteLine($"⚠️  WARNING: Chapter {chapterNum} has {chapterLength} characters, exceeds maximum of {maxChapterLength} for context {contextWindowSize}");
                    Console.WriteLine("   This may cause truncation or processing issues. Consider splitting this chapter or increasing context size.");
                }
            }

            for (int i = 0; i < chapterContents.Count; i++)
            {
                int currentChapter = i + 1; // Chapters are 1-based

                // Dynamically filter characters for this chapter based on IntroducedInChapter and LastChangedInChapter
                var availableCharacters = allCharacters.Where(c =>
                    c.IntroducedInChapter <= currentChapter &&
                    (c.LastChangedInChapter == null || c.LastChangedInChapter >= currentChapter)
                ).ToList();

                // Get summaries up to (but not including) this chapter
                var previousSummaries = allSummaries.Take(i).ToList();

                Console.WriteLine($"[Revision] Chapter {currentChapter}/{chapterContents.Count} - Revising...");
                Console.WriteLine($"  Using {availableCharacters.Count} characters and {previousSummaries.Count} summaries from previous chapters");

                var request = new ChapterProcessingRequest
                {
                    ModelName = modelName,
                    Temperature = temperature,
                    ContextWindowSize = contextWindowSize,
                    KnownCharacters = availableCharacters,
                    PreviousChapterSummaries = previousSummaries,
                    ChapterContent = chapterContents[i],
                    ChapterMarker = "",
                    Ruleset = ruleset
                };

                // Retry logic: if revision returns less than 200 characters, retry up to 3 times
                string revisedChapter = "";
                int maxRetries = 3;
                int attempt = 0;

                while (attempt < maxRetries)
                {
                    attempt++;
                    revisedChapter = await ReviseChapterAsync(request, availableCharacters, previousSummaries);
                    await UnloadModelAsync(modelName);
                    if (revisedChapter.Length >= 200 && revisedChapter.Length>request.ChapterContent.Length/2 && revisedChapter.Length<request.ChapterContent.Length*2)
                    {
                        // Success - chapter is long enough
                        break;
                    }
                    else if (attempt < maxRetries)
                    {
                        Console.WriteLine($"[Revision] Chapter {i + 1} returned only {revisedChapter.Length} chars (attempt {attempt}/{maxRetries}). Retrying...");
                    }
                    else
                    {
                        Console.WriteLine($"[Revision] Chapter {i + 1} returned only {revisedChapter.Length} chars after {maxRetries} attempts. Using last result.");
                    }
                }

                revisedChapters.Add(revisedChapter);

                Console.WriteLine($"[Revision] Chapter {i + 1} revised. Length: {revisedChapter.Length} chars");

                // Notify UI of progress with the revised chapter
                onChapterRevised?.Invoke(revisedChapter, currentChapter, chapterContents.Count);
               
            }

            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine("REVISION COMPLETE!");
            Console.WriteLine($"{new string('=', 60)}\n");

            // Final unload
        

            return revisedChapters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in chapter revision: {ex.Message}");

            // Try to unload model on error
            try { await UnloadModelAsync(modelName); } catch { }

            throw;
        }
    }

    // Keep the single chapter method for backward compatibility (if needed)
    [Obsolete("Use ProcessAllChaptersAsync for better consistency")]
    // public async Task<ChapterProcessingResponse> ProcessChapterAsync(ChapterProcessingRequest request)
    // {
    //     try
    //     {
    //         // Step 1: Revise the chapter using context from PREVIOUS chapters (known characters and summaries)
    //         var revisedChapter = await ReviseChapterAsync(request, request.KnownCharacters, request.PreviousChapterSummaries);
    //
    //         // Step 2: Extract/Update characters from the REVISED chapter
    //         var updatedCharacters = await ExtractCharactersFromRevisedAsync(revisedChapter, request);
    //
    //         // Step 3: Create summary of the REVISED chapter for future chapters
    //         var chapterSummary = await SummarizeRevisedChapterAsync(revisedChapter, request);
    //
    //         // Step 4: Unload the model to ensure clean state for next chapter
    //         Console.WriteLine("[ProcessChapter] Unloading model to ensure clean state for next chapter...");
    //         await UnloadModelAsync(request.ModelName);
    //
    //         return new ChapterProcessingResponse
    //         {
    //             RevisedChapter = revisedChapter,
    //             UpdatedCharacters = updatedCharacters,
    //             ChapterSummary = chapterSummary
    //         };
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error processing chapter with Ollama: {ex.Message}");
    //
    //         // Try to unload model even on error
    //         try
    //         {
    //             await UnloadModelAsync(request.ModelName);
    //         }
    //         catch { }
    //
    //         return new ChapterProcessingResponse
    //         {
    //             RevisedChapter = $"Error: {ex.Message}",
    //             UpdatedCharacters = request.KnownCharacters,
    //             ChapterSummary = "Error processing chapter"
    //         };
    //     }
    // }

    private async Task<List<CharacterInfo>> ExtractCharactersFromRawAsync(string rawChapter, ChapterProcessingRequest request)
    {
        Console.WriteLine("[REQUEST TYPE] Character Extraction (Raw)");

        var systemPrompt = @"You are a character analyst for stories. Your task is to identify all characters in a chapter and track their PHYSICAL APPEARANCE only.

!!!!! ABSOLUTELY CRITICAL RULES !!!!!
1. ONLY describe PHYSICAL APPEARANCE - NO actions, plot events, or story developments
2. NEVER REMOVE OR DELETE INFORMATION - You can only ADD appearance details
3. ALL existing appearance details MUST be preserved in your output
4. If a character isn't mentioned in the current chapter, KEEP THEM EXACTLY AS THEY WERE
5. Focus EXCLUSIVELY on what the character LOOKS LIKE, not what they DO
6. NEVER include role or chapter info in the 'name' field - names ONLY
7. NEVER create duplicate characters - ALWAYS match existing ones by name and role
8. IGNORE settings, locations, and objects - ONLY track living characters
9. Generic groups (""Goblins"", ""Orcs"", ""Tieflings"") are NOT individual characters - skip them
10. NEVER return empty descriptions - if you have no appearance info, use existing description
11. Keep descriptions CONCISE - maximum 250 characters per description

WHAT TO INCLUDE IN DESCRIPTION (APPEARANCE ONLY):
- Gender (male, female, non-binary, etc.)
- Age or age range (young, elderly, middle-aged, child, teen, etc.)
- Build/body type (tall, short, slender, muscular, stocky, etc.)
- Hair (color, length, style)
- Eyes (color, distinctive features)
- Skin tone
- Facial features (scars, beard, distinctive nose, etc.)
- Clothing/attire (what they typically wear)
- Distinctive physical marks (tattoos, scars, birthmarks)
- Species (if not human: elf, dwarf, alien, tiefling, etc.)

WHAT NOT TO INCLUDE:
- ❌ Personality traits (""brave"", ""kind"", ""cunning"")
- ❌ Actions (""helped the protagonist escape"")
- ❌ Relationships (""friend of Elvira"")
- ❌ Plot events (""revealed in chapter 2"")
- ❌ Skills or abilities (""skilled archer"", ""powerful mage"")
- ❌ Emotional states (""angry"", ""happy"")
- ❌ Settings/locations (""Baldur's Gate"", ""Tavern"", ""Inn"")
- ❌ Generic groups (""Goblins"", ""Orcs"", ""Soldiers"")
- ❌ Role information in name field (WRONG: ""Astarion (Rogue)"", CORRECT: name=""Astarion"", role=""Rogue"")

MATCHING EXISTING CHARACTERS:
Before creating a new character, check if they already exist:
1. Compare by name (exact match, case-insensitive)
2. If name matches, UPDATE existing entry - DO NOT create duplicate
3. If ""Unknown"" with same role exists, that might be this character
4. Only create NEW entry if you're certain it's a different character
5. When in doubt, UPDATE existing rather than create new

EXAMPLES:

Example 1 - Matching by Name:
Known: [{""name"": ""Shadowheart"", ""role"": ""Mage"", ""description"": ""Half-elf, brown hair""}]
Current: Shadowheart casts a spell
Result: [{""name"": ""Shadowheart"", ""role"": ""Mage"", ""description"": ""Half-elf, brown hair""}] (NO CHANGE - no new appearance info)

Example 2 - Name Revealed:
Known: [{""name"": ""Unknown"", ""role"": ""Narrator"", ""description"": ""Female, twenties, average height""}]
Current: Her name is Elvira
Result: [{""name"": ""Elvira"", ""role"": ""Narrator"", ""description"": ""Female, twenties, average height""}]

Example 3 - Adding Appearance (CORRECT):
Known: [{""name"": ""Elvira"", ""role"": ""Protagonist"", ""description"": ""Young woman, brown hair, green eyes""}]
Current: She wears a blue cloak and has a scar on her left cheek
Result: [{""name"": ""Elvira"", ""role"": ""Protagonist"", ""description"": ""Young woman, brown hair, green eyes, blue cloak, scar on left cheek""}]

Return as JSON object with a 'characters' array. DO NOT include this example structure in your actual response:
{
  ""characters"": [
    {
      ""name"": ""Elvira"",
      ""role"": ""Protagonist"",
      ""description"": ""Young woman, brown hair, green eyes""
    }
  ]
}";

        var userPrompt = new StringBuilder();

        if (request.KnownCharacters.Count > 0)
        {
            userPrompt.AppendLine("KNOWN CHARACTERS FROM PREVIOUS CHAPTERS:");
            foreach (var character in request.KnownCharacters)
            {
                userPrompt.AppendLine($"- {character.Name} ({character.Role}):");
                userPrompt.AppendLine($"  Description: {character.Description}");
            }
            userPrompt.AppendLine();
        }

        userPrompt.AppendLine("RAW CHAPTER TO ANALYZE:");
        userPrompt.AppendLine(rawChapter);
        userPrompt.AppendLine();
        userPrompt.AppendLine("Provide the updated character list as JSON object with 'characters' array:");

        var response = await CallOllamaAsync(request.ModelName, systemPrompt, userPrompt.ToString(), 0.3, request.ContextWindowSize, jsonFormat: true);

        try
        {
            var wrapper = JsonSerializer.Deserialize<CharacterListWrapper>(response);
            return wrapper?.Characters ?? request.KnownCharacters;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ExtractCharactersFromRaw] Deserialization error: {e.Message}");
            Console.WriteLine($"[ExtractCharactersFromRaw] Response was: {response}");
            return request.KnownCharacters;
        }
    }


    private async Task<string> SummarizeRawChapterAsync(string rawChapter, ChapterProcessingRequest request)
    {
        Console.WriteLine("[REQUEST TYPE] Chapter Summary (Raw)");

        var systemPrompt = @"You are a chapter summarizer for stories. Your task is to create a brief, concise summary of a chapter.

INSTRUCTIONS:
- Read the provided chapter
- Create a 2-3 sentence summary covering the main events and developments
- Focus on plot progression, character actions, and key revelations
- Keep it concise but informative
- Return ONLY the summary text, no JSON, no additional formatting";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine("RAW CHAPTER TO SUMMARIZE:");
        userPrompt.AppendLine(rawChapter);
        userPrompt.AppendLine();
        userPrompt.AppendLine("Provide a brief summary:");

        return await CallOllamaAsync(request.ModelName, systemPrompt, userPrompt.ToString(), 0.5, request.ContextWindowSize);
    }


    private async Task<string> ReviseChapterAsync(ChapterProcessingRequest request, List<CharacterInfo> characters, List<string> previousChapterSummaries)
    {
        Console.WriteLine("[REQUEST TYPE] Chapter Revision");

        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine("You are a professional writing assistant helping an author revise their story.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("YOUR ROLE:");
        systemPrompt.AppendLine("- You assist the writer by applying specific revisions and improvements to their chapter");
        systemPrompt.AppendLine("- You maintain consistency with the established story, characters, and tone");
        systemPrompt.AppendLine("- You preserve the author's voice and style while implementing requested changes");
        systemPrompt.AppendLine("- You rephrase sentences if this improves it or avoids repetition");
        systemPrompt.AppendLine("IMPORTANT: Rewrite the ENTIRE following chapter from start to finish. ");
        
        
        
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("CONTEXT PROVIDED TO HELP YOU:");
        systemPrompt.AppendLine("- Character List: Ensure consistency with established character traits and relationships");
        systemPrompt.AppendLine("- Previous Chapter Summaries: Maintain plot continuity and avoid contradictions");
        systemPrompt.AppendLine();

        if (characters.Count > 0)
        {
            systemPrompt.AppendLine("CHARACTERS IN THIS STORY (APPEARANCE ONLY):");
            foreach (var character in characters)
            {
                systemPrompt.AppendLine($"- {character.Name} ({character.Role}): {character.Description}");
            }
            systemPrompt.AppendLine();
        }

        if (previousChapterSummaries.Count > 0)
        {
            systemPrompt.AppendLine("PREVIOUS CHAPTERS:");
            for (int i = 0; i < previousChapterSummaries.Count; i++)
            {
                systemPrompt.AppendLine($"Chapter {i + 1}: {previousChapterSummaries[i]}");
            }
            systemPrompt.AppendLine();
        }

        systemPrompt.AppendLine("IMPORTANT:");
        systemPrompt.AppendLine("- The writer will provide specific instructions for what changes to make");
        systemPrompt.AppendLine("- Follow their instructions precisely");
        systemPrompt.AppendLine("- ALWAYS fix any typos, spelling errors, and grammatical mistakes");
        systemPrompt.AppendLine("- Maintain story consistency and tone throughout");
        systemPrompt.AppendLine("- Check for any inconsistencies with previous chapters and characters");
        systemPrompt.AppendLine("- Return the revised chapter with a chapter title in the format: ## Title");
        systemPrompt.AppendLine("- After the chapter title, provide the revised chapter text");
        systemPrompt.AppendLine("- Do not include explanations or meta-commentary, only the chapter title and text");

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine("WRITER'S INSTRUCTIONS:");
        userPrompt.AppendLine(request.Ruleset);
        userPrompt.AppendLine();
        userPrompt.AppendLine("CHAPTER TO REVISE:");
        userPrompt.AppendLine(request.ChapterContent);
        userPrompt.AppendLine();
        userPrompt.AppendLine("Provide the revised chapter with a title in the format '## Title' followed by the chapter text:");

        return await CallOllamaAsync(request.ModelName, systemPrompt.ToString(), userPrompt.ToString(), request.Temperature, request.ContextWindowSize);
    }

    private async Task<string> CallOllamaAsync(string model, string systemPrompt, string userPrompt, double temperature, int contextWindowSize = 4096, bool jsonFormat = false)
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
            //throw new Exception("Empty response from Ollama");
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

// Request/Response models
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

public class ModelInfo
{
    public int MaxContext { get; set; } = 0;  // 0 = not detected, makes failures obvious
    public double ModelSizeInBillions { get; set; } = 7.0;
    public double QuantizationBits { get; set; } = 4.5;
}

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

// Ollama API models
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
