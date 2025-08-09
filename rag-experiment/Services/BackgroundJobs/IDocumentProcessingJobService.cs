using rag_experiment.Models;
using System.Threading.Tasks;

namespace rag_experiment.Services.BackgroundJobs
{
    public interface IDocumentProcessingJobService
    {
        Task StartProcessing(string documentId, string filePath, string UserId, string ConversationId);
    }
}
