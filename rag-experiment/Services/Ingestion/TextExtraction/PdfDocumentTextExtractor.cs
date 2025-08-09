using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using rag_experiment.Services.Ingestion.TextExtraction;

namespace rag_experiment.Services
{
    public class PdfDocumentTextExtractor : ITextExtractor
    {
        /// <inheritdoc />
        public async Task<string> ExtractTextAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
                
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The specified file was not found.", filePath);
                
            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The specified file is not a PDF document.", nameof(filePath));
                
            return await ExtractTextFromPdfAsync(filePath);
        }

        public async Task<Dictionary<string, string>> ReadPdfFilesAsync(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"PDF documents directory not found at: {directoryPath}");
            }

            var pdfFiles = Directory.GetFiles(directoryPath, "*.pdf", SearchOption.AllDirectories);
            var result = new Dictionary<string, string>();

            foreach (var filePath in pdfFiles)
            {
                try
                {
                    // Read text from PDF using iText7
                    var content = await ExtractTextFromPdfAsync(filePath);
                    result[filePath] = content;
                }
                catch (Exception ex)
                {
                    // Log the error and continue with other files
                    Console.WriteLine($"Error reading PDF file {filePath}: {ex.Message}");
                }
            }

            return result;
        }
        
        private async Task<string> ExtractTextFromPdfAsync(string filePath)
        {
            // iText7 doesn't have built-in async methods for PDF parsing,
            // but we can wrap the synchronous operations in a Task for consistency
            return await Task.Run(() => 
            {
                var text = new StringBuilder();
                
                using (var pdfReader = new PdfReader(filePath))
                using (var pdfDocument = new PdfDocument(pdfReader))
                {
                    var numberOfPages = pdfDocument.GetNumberOfPages();
                    
                    for (int i = 1; i <= numberOfPages; i++)
                    {
                        var page = pdfDocument.GetPage(i);
                        var strategy = new SimpleTextExtractionStrategy();
                        var currentText = PdfTextExtractor.GetTextFromPage(page, strategy);
                        
                        text.AppendLine(currentText);
                    }
                }
                
                return text.ToString();
            });
        }
    }
} 