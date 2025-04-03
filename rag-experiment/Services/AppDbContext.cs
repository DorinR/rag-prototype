using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<Embedding> Embeddings { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
    }
} 