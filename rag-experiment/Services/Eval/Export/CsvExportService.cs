using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace rag_experiment.Services
{
    public class CsvExportService : ICsvExportService
    {
        private readonly AppDbContext _dbContext;
        private readonly string _defaultExportPath;

        public CsvExportService(AppDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            
            // Get export path from configuration or use default
            _defaultExportPath = configuration["CsvExportPath"] ?? Path.Combine("docs", "experiment_results.csv");
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_defaultExportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public async Task<string> ExportExperimentsToCSVAsync(string filePath = null)
        {
            // Use provided path or default
            string exportPath = !string.IsNullOrEmpty(filePath) ? filePath : _defaultExportPath;
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Get all experiments
            var experiments = await _dbContext.ExperimentResults.ToListAsync();
            
            // Build CSV content
            var csv = new StringBuilder();
            
            // Add header
            csv.AppendLine("Id,Timestamp,ExperimentName,Description,EmbeddingModelName,EmbeddingDimension," +
                          "ChunkSize,ChunkOverlap,StopwordRemoval,Stemming,Lemmatization,QueryExpansion," +
                          "TopK,AveragePrecision,AverageRecall,AverageF1Score,Notes");
            
            // Add rows
            foreach (var experiment in experiments)
            {
                csv.AppendLine(string.Join(",",
                    Escape(experiment.Id.ToString()),
                    Escape(experiment.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                    Escape(experiment.ExperimentName),
                    Escape(experiment.Description),
                    Escape(experiment.EmbeddingModelName),
                    Escape(experiment.EmbeddingDimension.ToString()),
                    Escape(experiment.ChunkSize.ToString()),
                    Escape(experiment.ChunkOverlap.ToString()),
                    Escape(experiment.StopwordRemoval.ToString()),
                    Escape(experiment.Stemming.ToString()),
                    Escape(experiment.Lemmatization.ToString()),
                    Escape(experiment.QueryExpansion.ToString()),
                    Escape(experiment.TopK.ToString()),
                    Escape(experiment.AveragePrecision.ToString("F4", CultureInfo.InvariantCulture)),
                    Escape(experiment.AverageRecall.ToString("F4", CultureInfo.InvariantCulture)),
                    Escape(experiment.AverageF1Score.ToString("F4", CultureInfo.InvariantCulture)),
                    Escape(experiment.Notes)
                ));
            }
            
            // Write to file
            await File.WriteAllTextAsync(exportPath, csv.ToString());
            
            return exportPath;
        }
        
        // Helper method to properly escape CSV fields
        private static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
                
            // Check if the field contains commas, quotes, or newlines
            bool needsQuotes = field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r");
            
            if (needsQuotes)
            {
                // Double up any quotes and wrap in quotes
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            
            return field;
        }
    }
} 