using System;
using System.Linq;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    public class EmbeddingService
    {
        private readonly AppDbContext _context;

        public EmbeddingService(AppDbContext context)
        {
            _context = context;
        }

        public void AddEmbedding(string text, float[] embeddingData)
        {
            var embedding = new Embedding
            {
                Text = text,
                EmbeddingData = ConvertToBlob(embeddingData)
            };

            _context.Embeddings.Add(embedding);
            _context.SaveChanges();
        }

        public (int Id, string Text, float[] EmbeddingVector) GetEmbedding(int id)
        {
            var embedding = _context.Embeddings.Find(id);
            if (embedding == null)
                return default;
                
            return (embedding.Id, embedding.Text, ConvertFromBlob(embedding.EmbeddingData));
        }

        public void UpdateEmbedding(int id, string newText, float[] newEmbeddingData)
        {
            var embedding = _context.Embeddings.Find(id);
            if (embedding != null)
            {
                embedding.Text = newText;
                embedding.EmbeddingData = ConvertToBlob(newEmbeddingData);
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