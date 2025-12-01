using System.Text.RegularExpressions;

namespace StoryOptimizer.Services;

public static class CleanService
{
     public static string CleanText(string text)
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