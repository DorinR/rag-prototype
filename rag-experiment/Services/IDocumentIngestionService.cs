using System.Collections.Generic;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    public interface IDocumentIngestionService
    {
        /// <summary>
        /// Ingests all markdown files from an Obsidian vault and processes them into chunks
        /// </summary>
        /// <param name="vaultPath">Path to the Obsidian vault directory</param>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <param name="overlap">Number of characters to overlap between chunks</param>
        /// <returns>Dictionary with file paths as keys and their chunks as values</returns>
        Task<Dictionary<string, List<string>>> IngestVaultAsync(string vaultPath, int maxChunkSize = 1000, int overlap = 100);
    }
} 