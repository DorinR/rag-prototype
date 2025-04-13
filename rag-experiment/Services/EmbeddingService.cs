using System;
using System.Linq;
using System.Collections.Generic;
using rag_experiment.Models;
using System.Numerics;

namespace rag_experiment.Services
{
    public class EmbeddingService
    {
        private readonly AppDbContext _context;

        public EmbeddingService(AppDbContext context)
        {
            _context = context;
        }

        public void AddEmbedding(string text, float[] embeddingData, string documentId = "", string documentTitle = "")
        {
            var embedding = new Embedding
            {
                Text = text,
                EmbeddingData = ConvertToBlob(embeddingData),
                DocumentId = documentId,
                DocumentTitle = documentTitle
            };

            _context.Embeddings.Add(embedding);
            _context.SaveChanges();
        }

        public (int Id, string Text, float[] EmbeddingVector, string DocumentId, string DocumentTitle) GetEmbedding(int id)
        {
            var embedding = _context.Embeddings.Find(id);
            if (embedding == null)
                return default;
                
            return (embedding.Id, embedding.Text, ConvertFromBlob(embedding.EmbeddingData), embedding.DocumentId, embedding.DocumentTitle);
        }

        public void UpdateEmbedding(int id, string newText, float[] newEmbeddingData, string documentId = null, string documentTitle = null)
        {
            var embedding = _context.Embeddings.Find(id);
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
            var embedding = _context.Embeddings.Find(id);
            if (embedding != null)
            {
                _context.Embeddings.Remove(embedding);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding.
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        public List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddings(float[] queryEmbedding, int topK = 10)
        {
            var results = new List<(string Text, string DocumentId, string DocumentTitle, float Similarity)>();
            
            // Load all embeddings from the database
            var embeddings = _context.Embeddings.ToList();
            
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
    }
} 