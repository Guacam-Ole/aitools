using System.Text;
using System.Text.Json;
using StoryOptimizer.Models;

namespace StoryOptimizer.Services;

public class StoryProcessingService
{
    private readonly OllamaApiService _ollamaApiService;

    public StoryProcessingService(OllamaApiService ollamaApiService)
    {
        _ollamaApiService = ollamaApiService;
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
                await _ollamaApiService.UnloadModelAsync(modelName);
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
                await _ollamaApiService.UnloadModelAsync(modelName);
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
                    await _ollamaApiService.UnloadModelAsync(modelName);
                    if (revisedChapter.Length >= 200 && revisedChapter.Length > request.ChapterContent.Length / 2 && revisedChapter.Length < request.ChapterContent.Length * 2)
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

            return revisedChapters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in chapter revision: {ex.Message}");

            // Try to unload model on error
            try { await _ollamaApiService.UnloadModelAsync(modelName); } catch { }

            throw;
        }
    }

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

        var response = await _ollamaApiService.CallOllamaAsync(request.ModelName, systemPrompt, userPrompt.ToString(), 0.3, request.ContextWindowSize, jsonFormat: true);

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

        return await _ollamaApiService.CallOllamaAsync(request.ModelName, systemPrompt, userPrompt.ToString(), 0.5, request.ContextWindowSize);
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

        return await _ollamaApiService.CallOllamaAsync(request.ModelName, systemPrompt.ToString(), userPrompt.ToString(), request.Temperature, request.ContextWindowSize);
    }
}
