using Microsoft.Data.Sqlite;
using SiemAgent.Models;
using System.Text.Json;

namespace SiemAgent.Services
{
    /// <summary>
    /// SQLite-based implementation of event caching service
    /// </summary>
    public class EventCacheService : IEventCacheService
    {
        private readonly ILogger<EventCacheService> _logger;
        private readonly string _connectionString;
        private readonly JsonSerializerOptions _jsonOptions;

        public EventCacheService(ILogger<EventCacheService> logger, IConfiguration configuration)
        {
            _logger = logger;
            var dbPath = configuration.GetValue<string>("EventCache:DatabasePath") ?? "events_cache.db";
            _connectionString = $"Data Source={dbPath}";
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS CachedEvents (
                        Id TEXT PRIMARY KEY,
                        EventData TEXT NOT NULL,
                        CachedAt DATETIME NOT NULL,
                        RetryCount INTEGER DEFAULT 0,
                        LastRetryAt DATETIME NULL
                    );
                    
                    CREATE INDEX IF NOT EXISTS IX_CachedEvents_CachedAt ON CachedEvents(CachedAt);
                    CREATE INDEX IF NOT EXISTS IX_CachedEvents_RetryCount ON CachedEvents(RetryCount);
                ";

                await createTableCommand.ExecuteNonQueryAsync();
                
                _logger.LogInformation("Event cache database initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize event cache database");
                return false;
            }
        }

        public async Task<bool> CacheEventsAsync(IEnumerable<SiemEvent> siemEvents)
        {
            var success = true;
            foreach (var siemEvent in siemEvents)
            {
                var result = await CacheEventAsync(siemEvent);
                if (!result)
                {
                    success = false;
                    break;
                }
            }
            return success;
        }

        public async Task<bool> CacheEventAsync(SiemEvent siemEvent)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO CachedEvents (Id, EventData, CachedAt, RetryCount)
                    VALUES (@id, @eventData, @cachedAt, @retryCount)
                ";

                command.Parameters.AddWithValue("@id", siemEvent.Id.ToString());
                command.Parameters.AddWithValue("@eventData", JsonSerializer.Serialize(siemEvent, _jsonOptions));
                command.Parameters.AddWithValue("@cachedAt", DateTime.UtcNow);
                command.Parameters.AddWithValue("@retryCount", siemEvent.RetryCount);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    siemEvent.IsCached = true;
                    _logger.LogDebug($"Cached event {siemEvent.Id}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cache event {siemEvent.Id}");
                return false;
            }
        }

        public async Task<IEnumerable<SiemEvent>> GetCachedEventsAsync(int batchSize = 100)
        {
            var events = new List<SiemEvent>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, EventData, RetryCount
                    FROM CachedEvents
                    ORDER BY CachedAt ASC
                    LIMIT @batchSize
                ";

                command.Parameters.AddWithValue("@batchSize", batchSize);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var eventData = reader.GetString(1); // EventData column
                        var siemEvent = JsonSerializer.Deserialize<SiemEvent>(eventData, _jsonOptions);
                        
                        if (siemEvent != null)
                        {
                            siemEvent.RetryCount = reader.GetInt32(2); // RetryCount column
                            siemEvent.IsCached = true;
                            events.Add(siemEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to deserialize cached event {reader.GetString(0)}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve cached events");
            }

            return events;
        }

        public async Task<bool> RemoveCachedEventAsync(Guid eventId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CachedEvents WHERE Id = @id";
                command.Parameters.AddWithValue("@id", eventId.ToString());

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    _logger.LogDebug($"Removed cached event {eventId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to remove cached event {eventId}");
                return false;
            }
        }

        public async Task<bool> RemoveCachedEventsAsync(IEnumerable<Guid> eventIds)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM CachedEvents WHERE Id = @id";
                
                var idParameter = command.CreateParameter();
                idParameter.ParameterName = "@id";
                command.Parameters.Add(idParameter);

                var removedCount = 0;
                foreach (var eventId in eventIds)
                {
                    idParameter.Value = eventId.ToString();
                    removedCount += await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                
                _logger.LogDebug($"Removed {removedCount} cached events");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove cached events");
                return false;
            }
        }

        public async Task<int> GetCachedEventCountAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM CachedEvents";

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cached event count");
                return 0;
            }
        }

        public async Task<bool> ClearCacheAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CachedEvents";

                await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("Cleared all cached events");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cached events");
                return false;
            }
        }

        public async Task<bool> CleanupExpiredEventsAsync(TimeSpan maxAge)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    DELETE FROM CachedEvents 
                    WHERE CachedAt < @cutoffTime
                ";

                var cutoffTime = DateTime.UtcNow - maxAge;
                command.Parameters.AddWithValue("@cutoffTime", cutoffTime);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    _logger.LogInformation($"Cleaned up {rowsAffected} expired cached events");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired cached events");
                return false;
            }
        }
    }
}
