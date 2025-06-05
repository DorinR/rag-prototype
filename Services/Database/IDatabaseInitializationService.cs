namespace rag_experiment.Services.Database
{
    public interface IDatabaseInitializationService
    {
        Task InitializeDatabaseAsync();
        Task<bool> IsDatabaseHealthyAsync();
        Task<int> GetPendingMigrationsCountAsync();
    }
}