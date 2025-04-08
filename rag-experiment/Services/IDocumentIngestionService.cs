using System.Collections.Generic;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    public interface IDocumentIngestionService
    {
        /// <summary>
        /// Ingests all markdown files from an Obsidian vault, processes them into chunks,
        /// and generates embeddings for each chunk
        /// </summary>
        /// <param name="vaultPath">Path to the Obsidian vault directory</param>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <param name="overlap">Number of characters to overlap between chunks</param>
        /// <returns>List of document embeddings ready to be stored in the vector database</returns>
        Task<List<DocumentEmbedding>> IngestVaultAsync(string vaultPath, int maxChunkSize = 1000, int overlap = 100);
        
        /// <summary>
        /// Ingests all papers from the CISI papers directory in Test Data, processes them into chunks,
        /// and generates embeddings for each chunk
        /// </summary>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <param name="overlap">Number of characters to overlap between chunks</param>
        /// <returns>List of document embeddings ready to be stored in the vector database</returns>
        Task<List<DocumentEmbedding>> IngestCisiPapersAsync(int maxChunkSize = 1000, int overlap = 100);
    }
} 