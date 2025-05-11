namespace rag_experiment.Services
{
    public interface ICsvExportService
    {
        /// <summary>
        /// Exports all experiment results to a CSV file
        /// </summary>
        /// <param name="filePath">Optional path for the CSV file. If not provided, a default path will be used.</param>
        /// <returns>The full path to the generated CSV file</returns>
        Task<string> ExportExperimentsToCSVAsync(string filePath = null);
    }
} 