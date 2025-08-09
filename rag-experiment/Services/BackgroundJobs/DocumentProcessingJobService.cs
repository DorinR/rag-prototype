using Hangfire;
using rag_experiment.Models;
using rag_experiment.Repositories;
using rag_experiment.Services.Ingestion.TextExtraction;
using rag_experiment.Services.Ingestion.VectorStorage;

namespace rag_experiment.Services.BackgroundJobs
{
    public class DocumentProcessingJobService : IDocumentProcessingJobService
    {
        private readonly IDocumentProcessingStateRepository _stateRepo;
        private readonly ITextExtractor _textExtractor;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingGenerationService _embeddingService;
        private readonly IEmbeddingRepository _embeddingRepository;

        public DocumentProcessingJobService(
            IDocumentProcessingStateRepository stateRepo,
            ITextExtractor textExtractor,
            ITextChunker textChunker,
            IEmbeddingGenerationService embeddingService,
            IEmbeddingRepository embeddingRepository)
        {
            _stateRepo = stateRepo;
            _textExtractor = textExtractor;
            _textChunker = textChunker;
            _embeddingService = embeddingService;
            _embeddingRepository = embeddingRepository;
        }

        public async Task StartProcessing(string documentId, string filePath)
        {
            var docId = int.Parse(documentId);

            // Initialize state
            var state = new DocumentProcessingState
            {
                DocumentId = docId,
                FilePath = filePath,
                Status = ProcessingStatus.Pending
            };
            await _stateRepo.SaveStateAsync(state);

            // Set up the entire job chain upfront using the driver method pattern
            var job1 = BackgroundJob.Enqueue<DocumentProcessingJobService>(
                x => x.ExtractText(docId));

            var job2 = BackgroundJob.ContinueJobWith<DocumentProcessingJobService>(
                job1, x => x.ProcessChunks(docId));

            var job3 = BackgroundJob.ContinueJobWith<DocumentProcessingJobService>(
                job2, x => x.GenerateEmbeddings(docId));

            var job4 = BackgroundJob.ContinueJobWith<DocumentProcessingJobService>(
                job3, x => x.PersistEmbeddings(docId));

            // Store the final job ID for tracking
            state.JobId = job4;
            await _stateRepo.SaveStateAsync(state);
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ExtractText(int documentId)
        {
            var state = await _stateRepo.GetStateAsync(documentId.ToString());
            try
            {
                state.ExtractedText = await _textExtractor.ExtractTextAsync(state.FilePath);
                state.Status = ProcessingStatus.TextExtracted;
                await _stateRepo.SaveStateAsync(state);
            }
            catch (Exception ex)
            {
                await HandleJobError(documentId, ex, "Failed to extract text");
                throw;
            }
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ProcessChunks(int documentId)
        {
            var state = await _stateRepo.GetStateAsync(documentId.ToString());
            try
            {
                // Use the correct method name - ChunkText, not SplitIntoChunksAsync
                state.Chunks = _textChunker.ChunkText(state.ExtractedText);
                state.Status = ProcessingStatus.ChunksCreated;
                await _stateRepo.SaveStateAsync(state);
            }
            catch (Exception ex)
            {
                await HandleJobError(documentId, ex, "Failed to process chunks");
                throw;
            }
        }

        [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 10, 30, 60, 120 })]
        public async Task GenerateEmbeddings(int documentId)
        {
            var state = await _stateRepo.GetStateAsync(documentId.ToString());
            try
            {
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(state.Chunks);

                // Convert the dictionary to a list of float arrays in the same order as chunks
                state.Embeddings = new List<float[]>();
                foreach (var chunk in state.Chunks)
                {
                    state.Embeddings.Add(embeddings[chunk]);
                }

                state.Status = ProcessingStatus.EmbeddingsGenerated;
                await _stateRepo.SaveStateAsync(state);
            }
            catch (Exception ex)
            {
                await HandleJobError(documentId, ex, "Failed to generate embeddings");
                throw;
            }
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task PersistEmbeddings(int documentId)
        {
            var state = await _stateRepo.GetStateAsync(documentId.ToString());
            try
            {
                // Use the correct method name - AddEmbedding, not SaveEmbeddingsAsync
                for (int i = 0; i < state.Chunks.Count; i++)
                {
                    _embeddingRepository.AddEmbedding(
                        text: state.Chunks[i],
                        embeddingData: state.Embeddings[i],
                        documentId: documentId.ToString(),
                        userId: 1, // TODO: Pass the actual user ID from the processing state
                        conversationId: 1, // TODO: Pass the actual conversation ID from the processing state
                        documentTitle: Path.GetFileName(state.FilePath)
                    );
                }

                state.Status = ProcessingStatus.Completed;
                await _stateRepo.SaveStateAsync(state);
            }
            catch (Exception ex)
            {
                await HandleJobError(documentId, ex, "Failed to persist embeddings");
                throw;
            }
        }

        private async Task HandleJobError(int documentId, Exception ex, string message)
        {
            var state = await _stateRepo.GetStateAsync(documentId.ToString());
            state.Status = ProcessingStatus.Failed;
            state.ErrorMessage = ex.Message;
            await _stateRepo.SaveStateAsync(state);
        }
    }
}