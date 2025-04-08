using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    public interface ICisiPapersReader
    {
        /// <summary>
        /// Reads all paper files from the CISI papers directory in Test Data
        /// </summary>
        /// <returns>Dictionary mapping file paths to file content</returns>
        Task<Dictionary<string, string>> ReadPapersAsync();
    }

    public class CisiPapersReader : ICisiPapersReader
    {
        private readonly string _cisiPapersPath = Path.Combine("Test Data", "cisi_papers");
        
        public async Task<Dictionary<string, string>> ReadPapersAsync()
        {
            if (!Directory.Exists(_cisiPapersPath))
            {
                throw new DirectoryNotFoundException($"CISI papers directory not found at: {_cisiPapersPath}");
            }

            // Get all files in the directory (assuming all files are valid papers)
            var paperFiles = Directory.GetFiles(_cisiPapersPath, "*.*", SearchOption.AllDirectories);
            var result = new Dictionary<string, string>();

            foreach (var filePath in paperFiles)
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