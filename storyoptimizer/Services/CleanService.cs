using System.Text.RegularExpressions;

namespace StoryOptimizer.Services;

public static class CleanService
{
     public static string CleanText(string text)
    {
        // Remove typical AI stuff:
        text = text.Replace("\u201C", "\""); 
        text = text.Replace("\u201D", "\""); 
        text = text.Replace("\u2018", "'");  
        text = text.Replace("\u2019", "'");  

        text = text.Replace("\u2013", "-"); 
        text = text.Replace("\u2014", "-"); 
        text = text.Replace("\u2212", "-"); 
        text = text.Replace("\u2010", "-"); 
        text = text.Replace("\u2011", "-"); 

        text = text.Replace("\u2026", "...");

        text = text.Replace("\u200B", ""); 
        text = text.Replace("\u200C", ""); 
        text = text.Replace("\u200D", ""); 
        text = text.Replace("\uFEFF", ""); 

        text = Regex.Replace(text, @"([^\n\r])  +", "$1 ");
        text = Regex.Replace(text, @" +([,.!?;:])", "$1");

        text = Regex.Replace(text, @"([.!?;:,])([A-Za-z])", "$1 $2");

        text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);

        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        text = text.Replace("\u2022", "-"); 
        text = text.Replace("\u25E6", "-"); 
        text = text.Replace("\u25AA", "-"); 
        text = text.Replace("\u25AB", "-"); 

        return text;
    }
}