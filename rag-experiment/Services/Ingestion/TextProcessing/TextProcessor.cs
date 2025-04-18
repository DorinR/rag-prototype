using System.Text.RegularExpressions;

namespace rag_experiment.Services
{
    public class TextProcessor : ITextProcessor
    {
        public string ProcessText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove Obsidian-specific syntax
            text = RemoveObsidianSyntax(text);
            
            // Normalize whitespace
            text = NormalizeWhitespace(text);
            
            // Remove any remaining special characters
            text = RemoveSpecialCharacters(text);

            return text.Trim();
        }

        private string RemoveObsidianSyntax(string text)
        {
            // Remove wiki-style links [[...]]
            text = Regex.Replace(text, @"\[\[([^\]]+)\]\]", "$1");
            
            // Remove markdown links [text](url)
            text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");
            
            // Remove code blocks
            text = Regex.Replace(text, @"```[\s\S]*?```", " ");
            
            // Remove inline code
            text = Regex.Replace(text, @"`[^`]+`", " ");

            return text;
        }

        private string NormalizeWhitespace(string text)
        {
            // Replace multiple spaces with single space
            text = Regex.Replace(text, @"\s+", " ");
            
            // Replace multiple newlines with single newline
            text = Regex.Replace(text, @"\n\s*\n", "\n");

            return text;
        }

        private string RemoveSpecialCharacters(string text)
        {
            // Keep alphanumeric characters, basic punctuation, and whitespace
            // Remove other special characters
            return Regex.Replace(text, @"[^\w\s.,!?-]", "");
        }
    }
} 