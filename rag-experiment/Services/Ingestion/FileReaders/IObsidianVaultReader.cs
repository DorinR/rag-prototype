namespace rag_experiment.Services
{
    public interface IObsidianVaultReader
    {
        /// <summary>
        /// Reads all markdown files from the specified Obsidian vault directory
        /// </summary>
        /// <param name="vaultPath">Path to the Obsidian vault directory</param>
        /// <returns>Dictionary with file paths as keys and their content as values</returns>
        Task<Dictionary<string, string>> ReadMarkdownFilesAsync(string vaultPath);
    }
} 