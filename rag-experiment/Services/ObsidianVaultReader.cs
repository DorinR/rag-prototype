using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    public class ObsidianVaultReader : IObsidianVaultReader
    {
        public async Task<Dictionary<string, string>> ReadMarkdownFilesAsync(string vaultPath)
        {
            if (!Directory.Exists(vaultPath))
            {
                throw new DirectoryNotFoundException($"Obsidian vault directory not found at: {vaultPath}");
            }

            var markdownFiles = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories);
            var result = new Dictionary<string, string>();

            foreach (var filePath in markdownFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    result[filePath] = content;
                }
                catch (IOException ex)
                {
                    // Log the error and continue with other files
                    Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
                }
            }

            return result;
        }
    }
} 