using System.Text;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    public class MarkdownTableService
    {
        private readonly string _markdownFilePath;
        
        public MarkdownTableService(string markdownFilePath = "experiment_results.md")
        {
            _markdownFilePath = markdownFilePath;
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(markdownFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Create the file with header if it doesn't exist
            if (!File.Exists(markdownFilePath))
            {
                InitializeMarkdownFile();
            }
        }
        
        private void InitializeMarkdownFile()
        {
            var content = new StringBuilder();
            content.AppendLine("# Experiment Results");
            content.AppendLine();
            content.AppendLine("This file is automatically generated and contains the results of all RAG experiments.");
            content.AppendLine();
            content.AppendLine("## Results Table");
            content.AppendLine();
            
            // Create the markdown table header
            content.AppendLine("| Name | Date | TopK | Precision | Recall | F1 Score | Chunk Size | Overlap | Model | Processing | Description |");
            content.AppendLine("| ---- | ---- | ---- | --------- | ------ | -------- | ---------- | ------- | ----- | ---------- | ----------- |");
            
            // Add the placeholder for experiment results
            content.AppendLine("<!-- EXPERIMENT_RESULTS -->");
            
            content.AppendLine();
            content.AppendLine("Last updated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            
            File.WriteAllText(_markdownFilePath, content.ToString());
        }
        
        public async Task AddExperimentToTableAsync(ExperimentResult experiment)
        {
            if (experiment == null)
                return;
                
            try
            {
                // Read the current content of the file
                string content = await File.ReadAllTextAsync(_markdownFilePath);
                
                // Generate the markdown row for the experiment
                string markdownRow = GenerateMarkdownRow(experiment);
                
                // Update markdown table
                if (content.Contains("<!-- EXPERIMENT_RESULTS -->"))
                {
                    // Split the content into parts before and after the marker
                    var parts = content.Split(new[] { "<!-- EXPERIMENT_RESULTS -->" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        // Reconstruct the content with proper line breaks
                        content = parts[0] + 
                                 "<!-- EXPERIMENT_RESULTS -->\n" + 
                                 markdownRow + "\n" + 
                                 parts[1];
                    }
                }
                
                // Update the "Last updated" line
                string oldUpdateLine = content.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("Last updated:"));
                
                if (!string.IsNullOrEmpty(oldUpdateLine))
                {
                    string newUpdateLine = "Last updated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
                    content = content.Replace(oldUpdateLine, newUpdateLine);
                }
                
                // Write the updated content back to the file
                await File.WriteAllTextAsync(_markdownFilePath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating markdown table: {ex.Message}");
                // Don't throw - this is a non-critical feature
            }
        }
        
        public async Task RegenerateTableFromExperimentsAsync(List<ExperimentResult> experiments)
        {
            if (experiments == null || !experiments.Any())
                return;
                
            try
            {
                // Sort experiments by timestamp (newest first)
                experiments = experiments.OrderByDescending(e => e.Timestamp).ToList();
                
                // Read the current content of the file
                string content = await File.ReadAllTextAsync(_markdownFilePath);
                
                // Generate all markdown rows
                var markdownRows = new StringBuilder();
                
                foreach (var experiment in experiments)
                {
                    markdownRows.AppendLine(GenerateMarkdownRow(experiment));
                }
                
                // Replace the markdown placeholder
                if (content.Contains("<!-- EXPERIMENT_RESULTS -->"))
                {
                    // Split the content into parts before and after the marker
                    var parts = content.Split(new[] { "<!-- EXPERIMENT_RESULTS -->" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        // Reconstruct the content with proper line breaks
                        content = parts[0] + 
                                 "<!-- EXPERIMENT_RESULTS -->\n" + 
                                 markdownRows.ToString().TrimEnd() + "\n" + 
                                 parts[1];
                    }
                }
                
                // Update the "Last updated" line
                string oldUpdateLine = content.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("Last updated:"));
                
                if (!string.IsNullOrEmpty(oldUpdateLine))
                {
                    string newUpdateLine = "Last updated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
                    content = content.Replace(oldUpdateLine, newUpdateLine);
                }
                
                // Write the updated content back to the file
                await File.WriteAllTextAsync(_markdownFilePath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error regenerating markdown table: {ex.Message}");
                // Don't throw - this is a non-critical feature
            }
        }
        
        private string GenerateMarkdownRow(ExperimentResult experiment)
        {
            // Format metrics with 3 decimal places
            string precision = experiment.AveragePrecision.ToString("F3");
            string recall = experiment.AverageRecall.ToString("F3");
            string f1Score = experiment.AverageF1Score.ToString("F3");
            
            // Format date
            string formattedDate = experiment.Timestamp.ToString("yyyy-MM-dd");
            
            // Generate text processing flags
            string textProcessing = GetTextProcessingFlags(experiment);
            
            // Truncate description if needed
            string description = TruncateWithEllipsis(experiment.Description, 30);
            
            // Escape pipe characters for markdown tables
            string escapedName = experiment.ExperimentName.Replace("|", "\\|");
            string escapedDescription = description.Replace("|", "\\|");
            string escapedModel = experiment.EmbeddingModelName.Replace("|", "\\|");
            
            // Build the markdown table row
            return $"| {escapedName} | {formattedDate} | {experiment.TopK} | {precision} | {recall} | {f1Score} | {experiment.ChunkSize} | {experiment.ChunkOverlap} | {escapedModel} | {textProcessing} | {escapedDescription} |";
        }
        
        private string GetTextProcessingFlags(ExperimentResult experiment)
        {
            var flags = new List<string>();
            
            if (experiment.StopwordRemoval) flags.Add("SW");
            if (experiment.Stemming) flags.Add("ST");
            if (experiment.Lemmatization) flags.Add("LM");
            if (experiment.QueryExpansion) flags.Add("QE");
            
            return flags.Count > 0 ? string.Join(",", flags) : "none";
        }
        
        private string TruncateWithEllipsis(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
                
            return text.Substring(0, maxLength - 3) + "...";
        }
    }
} 