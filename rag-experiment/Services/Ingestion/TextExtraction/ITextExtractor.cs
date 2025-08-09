using System.Threading.Tasks;

namespace rag_experiment.Services.Ingestion.TextExtraction;

/// <summary>
/// Defines the contract for text extraction from various document formats.
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text content from a document file asynchronously.
    /// </summary>
    /// <param name="filePath">The path to the document file.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the extracted text.</returns>
    Task<string> ExtractTextAsync(string filePath);
}
