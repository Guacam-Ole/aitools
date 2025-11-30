using System.Text;
using System.Text.RegularExpressions;

namespace AiFix;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Length > 2)
        {
            Console.WriteLine("Usage: AiFix <directory_path> [--no-recursive]");
            Console.WriteLine();
            Console.WriteLine("Cleans all *.md and *.txt files in the specified directory");
            Console.WriteLine("Output files are named: <original>.cleaned.md or <original>.cleaned.txt");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --no-recursive    Process only files in the top-level directory (default: recursive)");
            return 1;
        }

        var directory = args[0];
        var recursive = true;

        // Check for --no-recursive flag
        if (args.Length == 2)
        {
            if (args[1] == "--no-recursive")
            {
                recursive = false;
            }
            else
            {
                Console.WriteLine($"Error: Unknown option '{args[1]}'");
                Console.WriteLine("Use --no-recursive to disable recursive search");
                return 1;
            }
        }

        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"Error: '{directory}' is not a valid directory");
            return 1;
        }

        var files = GetFilesToProcess(directory, recursive);

        if (files.Count == 0)
        {
            Console.WriteLine($"No .md or .txt files found in '{directory}'");
            return 0;
        }

        Console.WriteLine($"Found {files.Count} file(s) to process\n");

        var successCount = 0;
        var errorCount = 0;

        foreach (var file in files)
        {
            try
            {
                ProcessFile(file);
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {Path.GetFileName(file)}: {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine("Processing complete!");
        Console.WriteLine($"Successfully processed: {successCount}");
        Console.WriteLine($"Errors: {errorCount}");
        Console.WriteLine($"{new string('=', 60)}");

        return 0;
    }

    static List<string> GetFilesToProcess(string directory, bool recursive)
    {
        var files = new List<string>();
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Get all .md files (excluding .cleaned.md)
        var mdFiles = Directory.GetFiles(directory, "*.md", searchOption)
            .Where(f => !Path.GetFileName(f).Contains(".cleaned."));

        // Get all .txt files (excluding .cleaned.txt)
        var txtFiles = Directory.GetFiles(directory, "*.txt", searchOption)
            .Where(f => !Path.GetFileName(f).Contains(".cleaned."));

        files.AddRange(mdFiles);
        files.AddRange(txtFiles);

        return files;
    }

    static void ProcessFile(string filePath)
    {
        // Read the file content
        var content = File.ReadAllText(filePath, Encoding.UTF8);

        // Clean the text
        var cleanedContent = CleanText(content);

        // Generate output filename
        var directory = Path.GetDirectoryName(filePath) ?? "";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var outputPath = Path.Combine(directory, $"{fileNameWithoutExtension}.cleaned{extension}");

        // Write cleaned content
        File.WriteAllText(outputPath, cleanedContent, Encoding.UTF8);

        // Report result
        Console.WriteLine(content == cleanedContent
            ? $"No changes needed: {Path.GetFileName(filePath)}"
            : $"Cleaned: {Path.GetFileName(filePath)} -> {Path.GetFileName(outputPath)}");
    }

    static string CleanText(string text)
    {
        // Fix smart quotes to regular quotes
        text = text.Replace("\u201C", "\""); // left double quotation mark
        text = text.Replace("\u201D", "\""); // right double quotation mark
        text = text.Replace("\u2018", "'");  // left single quotation mark
        text = text.Replace("\u2019", "'");  // right single quotation mark

        // Fix various types of dashes to regular hyphens
        text = text.Replace("\u2013", "-"); // en dash
        text = text.Replace("\u2014", "-"); // em dash
        text = text.Replace("\u2212", "-"); // minus sign
        text = text.Replace("\u2010", "-"); // hyphen
        text = text.Replace("\u2011", "-"); // non-breaking hyphen

        // Fix ellipsis
        text = text.Replace("\u2026", "...");

        // Remove zero-width spaces and other invisible characters
        text = text.Replace("\u200B", ""); // zero-width space
        text = text.Replace("\u200C", ""); // zero-width non-joiner
        text = text.Replace("\u200D", ""); // zero-width joiner
        text = text.Replace("\uFEFF", ""); // zero-width no-break space (BOM)

        // Fix multiple spaces to single space (but preserve intentional indentation)
        // Only fix multiple spaces that aren't at the start of a line
        text = Regex.Replace(text, @"([^\n\r])  +", "$1 ");

        // Fix spaces before punctuation
        text = Regex.Replace(text, @" +([,.!?;:])", "$1");

        // Fix missing space after punctuation (but not for numbers or ellipsis)
        text = Regex.Replace(text, @"([.!?;:,])([A-Za-z])", "$1 $2");

        // Remove trailing whitespace from lines
        text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);

        // Fix multiple blank lines (more than 2 consecutive newlines)
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Fix common Unicode bullets to standard characters
        text = text.Replace("\u2022", "-"); // bullet
        text = text.Replace("\u25E6", "-"); // white bullet
        text = text.Replace("\u25AA", "-"); // black small square
        text = text.Replace("\u25AB", "-"); // white small square

        return text;
    }
}
