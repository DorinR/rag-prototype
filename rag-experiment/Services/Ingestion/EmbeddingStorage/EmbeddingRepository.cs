using rag_experiment.Models;
using rag_experiment.Services.Auth;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace rag_experiment.Services.Ingestion.VectorStorage
{
    public class EmbeddingRepository : IEmbeddingRepository
    {
        private readonly AppDbContext _context;
        private readonly IUserContext _userContext;

        public EmbeddingRepository(AppDbContext context, IUserContext userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        public void AddEmbedding(string text, float[] embeddingData, string documentId, int userId, int conversationId, string documentTitle)
        {
            var embedding = new Embedding
            {
                Text = text,
                EmbeddingData = ConvertToBlob(embeddingData),
                DocumentId = documentId,
                DocumentTitle = documentTitle,
                UserId = userId,
                ConversationId = conversationId
            };

            _context.Embeddings.Add(embedding);
            _context.SaveChanges();
        }

        public (int Id, string Text, float[] EmbeddingVector, string DocumentId, string DocumentTitle) GetEmbedding(int id)
        {
            var userId = _userContext.GetCurrentUserId();

            var embedding = _context.Embeddings
                .FirstOrDefault(e => e.Id == id && e.UserId == userId);

            if (embedding == null)
                return default;

            return (embedding.Id, embedding.Text, ConvertFromBlob(embedding.EmbeddingData), embedding.DocumentId, embedding.DocumentTitle);
        }

        public void UpdateEmbedding(int id, string newText, float[] newEmbeddingData, string documentId = null, string documentTitle = null)
        {
            var userId = _userContext.GetCurrentUserId();

            var embedding = _context.Embeddings
                .FirstOrDefault(e => e.Id == id && e.UserId == userId);

            if (embedding != null)
            {
                embedding.Text = newText;
                embedding.EmbeddingData = ConvertToBlob(newEmbeddingData);

                if (documentId != null)
                {
                    embedding.DocumentId = documentId;
                }

                if (documentTitle != null)
                {
                    embedding.DocumentTitle = documentTitle;
                }

                _context.SaveChanges();
            }
        }

        public void DeleteEmbedding(int id)
        {
            var userId = _userContext.GetCurrentUserId();

            var embedding = _context.Embeddings
                .FirstOrDefault(e => e.Id == id && e.UserId == userId);

            if (embedding != null)
            {
                _context.Embeddings.Remove(embedding);
                _context.SaveChanges();
            }
        }

        public void DeleteEmbeddingsByDocumentId(string documentId)
        {
            var userId = _userContext.GetCurrentUserId();

            var embeddingsToDelete = _context.Embeddings
                .Where(e => e.DocumentId == documentId && e.UserId == userId)
                .ToList();

            if (embeddingsToDelete.Any())
            {
                _context.Embeddings.RemoveRange(embeddingsToDelete);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding, scoped to a conversation.
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="conversationId">The conversation ID to scope the search to</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        public List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddings(float[] queryEmbedding, int conversationId, int topK = 10)
        {
            var userId = _userContext.GetCurrentUserId();
            var results = new List<(string Text, string DocumentId, string DocumentTitle, float Similarity)>();

            // Load all embeddings from the database for the current user and conversation
            var embeddings = _context.Embeddings
                .Where(e => e.UserId == userId && e.ConversationId == conversationId)
                .ToList();

            // Calculate similarity for each embedding
            foreach (var embedding in embeddings)
            {
                var embeddingVector = ConvertFromBlob(embedding.EmbeddingData);
                var similarity = CosineSimilarity(queryEmbedding, embeddingVector);

                results.Add((embedding.Text, embedding.DocumentId, embedding.DocumentTitle, similarity));
            }

            // Return top K results, ordered by similarity (highest first)
            return results
                .OrderByDescending(r => r.Similarity)
                .Take(topK)
                .ToList();
        }

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding across all user's conversations.
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        public List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddingsAllConversations(float[] queryEmbedding, int topK = 10)
        {
            var userId = _userContext.GetCurrentUserId();
            var results = new List<(string Text, string DocumentId, string DocumentTitle, float Similarity)>();

            // Load all embeddings from the database for the current user across all conversations
            var embeddings = _context.Embeddings
                .Where(e => e.UserId == userId)
                .ToList();

            // Calculate similarity for each embedding
            foreach (var embedding in embeddings)
            {
                var embeddingVector = ConvertFromBlob(embedding.EmbeddingData);
                var similarity = CosineSimilarity(queryEmbedding, embeddingVector);

                results.Add((embedding.Text, embedding.DocumentId, embedding.DocumentTitle, similarity));
            }

            // Return top K results, ordered by similarity (highest first)
            return results
                .OrderByDescending(r => r.Similarity)
                .Take(topK)
                .ToList();
        }

        /// <summary>
        /// Calculates the cosine similarity between two embedding vectors.
        /// </summary>
        /// <param name="a">First embedding vector</param>
        /// <param name="b">Second embedding vector</param>
        /// <returns>Cosine similarity score between 0 and 1, or 0 if vectors are different lengths</returns>
        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                return 0; // Return minimum similarity score instead of throwing exception

            float dotProduct = 0;
            float normA = 0;
            float normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            // Handle zero vectors
            if (normA == 0 || normB == 0)
                return 0;

            return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private byte[] ConvertToBlob(float[] embeddingData)
        {
            // Convert the float array to a byte array
            // Each float is 4 bytes
            byte[] blob = new byte[embeddingData.Length * sizeof(float)];

            // Copy the float array to the byte array
            Buffer.BlockCopy(embeddingData, 0, blob, 0, blob.Length);

            return blob;
        }

        private float[] ConvertFromBlob(byte[] blob)
        {
            // Convert the byte array back to a float array
            float[] embeddingData = new float[blob.Length / sizeof(float)];

            // Copy the byte array to the float array
            Buffer.BlockCopy(blob, 0, embeddingData, 0, blob.Length);

            return embeddingData;
        }

        public async Task UpsertEmbeddingsAsync(IEnumerable<EmbeddingUpsertItem> items, CancellationToken cancellationToken = default)
        {
            // Group by scope to minimize queries
            var itemsByScope = items
                .GroupBy(i => new { i.UserId, i.ConversationId, i.DocumentId })
                .ToList();

            foreach (var scopeGroup in itemsByScope)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scope = scopeGroup.Key;

                // Prefetch existing rows for this scope
                var existing = await _context.Embeddings
                    .Where(e => e.UserId == scope.UserId
                                && e.ConversationId == scope.ConversationId
                                && e.DocumentId == scope.DocumentId)
                    .Select(e => new { e.ChunkIndex, e.Id, e.ChunkHash })
                    .ToListAsync(cancellationToken);

                var existingByIndex = existing.ToDictionary(x => x.ChunkIndex, x => x);

                var toInsert = new List<Embedding>();
                foreach (var item in scopeGroup)
                {
                    if (!existingByIndex.TryGetValue(item.ChunkIndex, out var existingRow))
                    {
                        toInsert.Add(new Embedding
                        {
                            Text = item.Text,
                            EmbeddingData = ConvertToBlob(item.Vector),
                            DocumentId = item.DocumentId,
                            DocumentTitle = item.DocumentTitle ?? string.Empty,
                            UserId = item.UserId,
                            ConversationId = item.ConversationId,
                            ChunkIndex = item.ChunkIndex,
                            ChunkHash = item.ChunkHash
                        });
                    }
                    else
                    {
                        // Update only if content changed (hash differs)
                        if (existingRow.ChunkHash == null || !item.ChunkHash.SequenceEqual(existingRow.ChunkHash))
                        {
                            var entity = await _context.Embeddings.FirstAsync(e => e.Id == existingRow.Id, cancellationToken);
                            entity.Text = item.Text;
                            entity.EmbeddingData = ConvertToBlob(item.Vector);
                            entity.DocumentTitle = item.DocumentTitle ?? entity.DocumentTitle;
                            entity.ChunkHash = item.ChunkHash;
                        }
                    }
                }

                if (toInsert.Count > 0)
                {
                    await _context.Embeddings.AddRangeAsync(toInsert, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}