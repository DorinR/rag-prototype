using rag_experiment.Models;
using System.Collections.Concurrent;

namespace rag_experiment.Repositories
{
    public class InMemoryDocumentProcessingStateRepository : IDocumentProcessingStateRepository
    {
        private readonly ConcurrentDictionary<string, DocumentProcessingState> _states = new();

        public Task<DocumentProcessingState> GetStateAsync(string documentId)
        {
            if (_states.TryGetValue(documentId, out var state))
                return Task.FromResult(state);
            throw new KeyNotFoundException($"No state found for document {documentId}");
        }

        public Task SaveStateAsync(DocumentProcessingState state)
        {
            _states[state.DocumentId.ToString()] = state;
            return Task.CompletedTask;
        }
    }
}
