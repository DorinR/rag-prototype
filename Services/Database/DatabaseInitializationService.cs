using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;

namespace rag_experiment.Services.Database
{
    public class DatabaseInitializationService : IDatabaseInitializationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseInitializationService> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseInitializationService(
            AppDbContext context,
            ILogger<DatabaseInitializationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization...");

                // Check if database exists
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    _logger.LogInformation("Database does not exist, creating...");
                }

                // Get pending migrations
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                var pendingCount = pendingMigrations.Count();

                if (pendingCount > 0)
                {
                    _logger.LogInformation($"Found {pendingCount} pending migrations: {string.Join(", ", pendingMigrations)}");

                    // Apply migrations
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("Successfully applied all pending migrations");
                }
                else
                {
                    _logger.LogInformation("Database is up to date, no migrations needed");
                }

                // Verify database health
                var isHealthy = await IsDatabaseHealthyAsync();
                if (!isHealthy)
                {
                    throw new InvalidOperationException("Database health check failed after initialization");
                }

                _logger.LogInformation("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");

                // In production, you might want to retry or fail gracefully
                // For now, we'll rethrow to prevent app startup
                throw new InvalidOperationException("Database initialization failed. See logs for details.", ex);
            }
        }

        public async Task<bool> IsDatabaseHealthyAsync()
        {
            try
            {
                // Try to connect and execute a simple query
                await _context.Database.CanConnectAsync();

                // Verify core tables exist
                var userTableExists = await _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM sqlite_master WHERE type='table' AND name='Users'").FirstOrDefaultAsync() > 0;
                var conversationTableExists = await _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) as Value FROM sqlite_master WHERE type='table' AND name='Conversations'").FirstOrDefaultAsync() > 0;

                return userTableExists && conversationTableExists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return false;
            }
        }

        public async Task<int> GetPendingMigrationsCountAsync()
        {
            try
            {
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                return pendingMigrations.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending migrations count");
                return -1; // Indicates error
            }
        }
    }
}