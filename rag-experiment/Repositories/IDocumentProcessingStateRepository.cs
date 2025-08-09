using rag_experiment.Models;

namespace rag_experiment.Repositories
{
    public interface IDocumentProcessingStateRepository
    {
        Task<DocumentProcessingState> GetStateAsync(string documentId);
        Task SaveStateAsync(DocumentProcessingState state);
    }
}
