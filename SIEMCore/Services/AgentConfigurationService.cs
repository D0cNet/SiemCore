using SiemCore.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SiemCore.Services
{
    /// <summary>
    /// Service for managing SIEM agent configurations
    /// </summary>
    public class AgentConfigurationService : IAgentConfigurationService
    {
        private readonly ILogger<AgentConfigurationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, AgentConfiguration> _agentConfigurations;
        private readonly ConcurrentDictionary<string, List<ConfigurationUpdateHistory>> _configurationHistory;
        private AgentConfiguration _defaultConfiguration;
        private readonly JsonSerializerOptions _jsonOptions;

        public AgentConfigurationService(
            ILogger<AgentConfigurationService> logger,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
            _agentConfigurations = new ConcurrentDictionary<string, AgentConfiguration>();
            _configurationHistory = new ConcurrentDictionary<string, List<ConfigurationUpdateHistory>>();
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            _defaultConfiguration = LoadDefaultConfiguration();
        }

        public async Task<AgentConfiguration?> GetAgentConfigurationAsync(string agentId)
        {
            try
            {
                if (_agentConfigurations.TryGetValue(agentId, out var configuration))
                {
                    _logger.LogDebug("Retrieved configuration for agent {AgentId}", agentId);
                    return configuration;
                }

                // Return default configuration if agent-specific config not found
                _logger.LogInformation("No specific configuration found for agent {AgentId}, returning default", agentId);
                return await GetDefaultConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration for agent {AgentId}", agentId);
                return null;
            }
        }

        public async Task<bool> UpdateAgentConfigurationAsync(string agentId, AgentConfiguration configuration)
        {
            try
            {
                // Validate configuration first
                var validationResult = await ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Invalid configuration for agent {AgentId}: {Errors}", 
                        agentId, string.Join(", ", validationResult.Errors));
                    return false;
                }

                var previousConfig = _agentConfigurations.TryGetValue(agentId, out var existing) ? existing : null;
                
                // Update the configuration
                configuration.AgentId = agentId;
                configuration.UpdatedAt = DateTime.UtcNow;
                _agentConfigurations.AddOrUpdate(agentId, configuration, (key, oldValue) => configuration);

                // Record the update in history
                await RecordConfigurationUpdateAsync(agentId, previousConfig, configuration, "Manual Update", true);

                _logger.LogInformation("Updated configuration for agent {AgentId}", agentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration for agent {AgentId}", agentId);
                return false;
            }
        }

        public async Task<AgentConfiguration> GetDefaultConfigurationAsync()
        {
            return await Task.FromResult(_defaultConfiguration);
        }

        public async Task<bool> UpdateDefaultConfigurationAsync(AgentConfiguration configuration)
        {
            try
            {
                var validationResult = await ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Invalid default configuration: {Errors}", 
                        string.Join(", ", validationResult.Errors));
                    return false;
                }

                _defaultConfiguration = configuration;
                _logger.LogInformation("Updated default agent configuration");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating default configuration");
                return false;
            }
        }

        public async Task<IEnumerable<AgentConfiguration>> GetAllAgentConfigurationsAsync()
        {
            return await Task.FromResult(_agentConfigurations.Values.ToList());
        }

        public async Task<AgentConfiguration> RegisterAgentAsync(string agentId, AgentRegistrationInfo agentInfo)
        {
            try
            {
                var configuration = await GetDefaultConfigurationAsync();
                configuration.AgentId = agentId;
                configuration.AgentVersion = agentInfo.AgentVersion;
                configuration.UpdatedAt = DateTime.UtcNow;

                // Store agent-specific configuration
                _agentConfigurations.TryAdd(agentId, configuration);

                // Record registration
                await RecordConfigurationUpdateAsync(agentId, null, configuration, "Agent Registration", true);

                _logger.LogInformation("Registered new agent {AgentId} with version {Version} from {IpAddress}", 
                    agentId, agentInfo.AgentVersion, agentInfo.IpAddress);

                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering agent {AgentId}", agentId);
                throw;
            }
        }

        public async Task<bool> RemoveAgentAsync(string agentId)
        {
            try
            {
                var removed = _agentConfigurations.TryRemove(agentId, out var configuration);
                if (removed)
                {
                    await RecordConfigurationUpdateAsync(agentId, configuration, null, "Agent Removal", true);
                    _logger.LogInformation("Removed agent {AgentId}", agentId);
                }
                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing agent {AgentId}", agentId);
                return false;
            }
        }

        public async Task<bool> PushConfigurationToAgentAsync(string agentId, AgentConfiguration configuration)
        {
            try
            {
                // Get agent's current endpoint (this would typically come from agent registry)
                var agentEndpoint = GetAgentEndpoint(agentId);
                if (string.IsNullOrEmpty(agentEndpoint))
                {
                    _logger.LogWarning("No endpoint found for agent {AgentId}, cannot push configuration", agentId);
                    return false;
                }

                // Prepare the configuration push request
                var json = JsonSerializer.Serialize(configuration, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Send configuration to agent
                var response = await _httpClient.PostAsync($"{agentEndpoint}/api/configuration/update", content);

                var success = response.IsSuccessStatusCode;
                var errorMessage = success ? null : $"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";

                // Record the push attempt
                await RecordConfigurationUpdateAsync(agentId, null, configuration, "Configuration Push", success, errorMessage);

                if (success)
                {
                    _logger.LogInformation("Successfully pushed configuration to agent {AgentId}", agentId);
                }
                else
                {
                    _logger.LogWarning("Failed to push configuration to agent {AgentId}: {Error}", agentId, errorMessage);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing configuration to agent {AgentId}", agentId);
                await RecordConfigurationUpdateAsync(agentId, null, configuration, "Configuration Push", false, ex.Message);
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> PushConfigurationToAllAgentsAsync(AgentConfiguration configuration)
        {
            var results = new Dictionary<string, bool>();
            var tasks = new List<Task>();

            foreach (var agentId in _agentConfigurations.Keys)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var success = await PushConfigurationToAgentAsync(agentId, configuration);
                    lock (results)
                    {
                        results[agentId] = success;
                    }
                }));
            }

            await Task.WhenAll(tasks);

            var successCount = results.Values.Count(r => r);
            _logger.LogInformation("Pushed configuration to {SuccessCount}/{TotalCount} agents", 
                successCount, results.Count);

            return results;
        }

        public async Task<IEnumerable<ConfigurationUpdateHistory>> GetConfigurationHistoryAsync(string agentId)
        {
            if (_configurationHistory.TryGetValue(agentId, out var history))
            {
                return await Task.FromResult(history.OrderByDescending(h => h.UpdatedAt));
            }
            return await Task.FromResult(Enumerable.Empty<ConfigurationUpdateHistory>());
        }

        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(AgentConfiguration configuration)
        {
            var result = new ConfigurationValidationResult { IsValid = true };

            try
            {
                // Validate required fields
                if (string.IsNullOrEmpty(configuration.AgentVersion))
                {
                    result.Errors.Add("AgentVersion is required");
                }

                // Validate numeric ranges
                if (configuration.EventBatchSize <= 0 || configuration.EventBatchSize > 10000)
                {
                    result.Errors.Add("EventBatchSize must be between 1 and 10000");
                }

                if (configuration.EventFlushIntervalSeconds <= 0 || configuration.EventFlushIntervalSeconds > 3600)
                {
                    result.Errors.Add("EventFlushIntervalSeconds must be between 1 and 3600");
                }

                if (configuration.MaxRetryAttempts < 0 || configuration.MaxRetryAttempts > 10)
                {
                    result.Errors.Add("MaxRetryAttempts must be between 0 and 10");
                }

                if (configuration.MaxCachedEvents <= 0 || configuration.MaxCachedEvents > 1000000)
                {
                    result.Errors.Add("MaxCachedEvents must be between 1 and 1000000");
                }

                // Validate log level
                var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
                if (!validLogLevels.Contains(configuration.LogLevel))
                {
                    result.Errors.Add($"LogLevel must be one of: {string.Join(", ", validLogLevels)}");
                }

                // Add warnings for potentially problematic settings
                if (configuration.EventBatchSize > 1000)
                {
                    result.Warnings.Add("Large EventBatchSize may impact memory usage");
                }

                if (configuration.EventFlushIntervalSeconds > 300)
                {
                    result.Warnings.Add("Long EventFlushInterval may delay event processing");
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration");
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
            }

            return await Task.FromResult(result);
        }

        private AgentConfiguration LoadDefaultConfiguration()
        {
            // Load default configuration from appsettings or use hardcoded defaults
            var defaultConfig = _configuration.GetSection("DefaultAgentConfiguration").Get<AgentConfiguration>();
            
            return defaultConfig ?? new AgentConfiguration
            {
                AgentId = "",
                AgentVersion = "1.0.0",
                EventBatchSize = 100,
                EventFlushIntervalSeconds = 30,
                MaxRetryAttempts = 3,
                RetryDelaySeconds = 5,
                MaxCachedEvents = 10000,
                EnableLocalAnalysis = true,
                EnableEventFiltering = true,
                LogLevel = "Information",
                HealthCheckIntervalSeconds = 60,
                ConfigurationRefreshIntervalSeconds = 300,
                SiemCoreApiUrl = "https://localhost:5001",
                ApiKey = "",
                UpdatedAt = DateTime.UtcNow
            };
        }

        private string? GetAgentEndpoint(string agentId)
        {
            // In a real implementation, this would query an agent registry
            // For now, return null to indicate push is not available
            // This could be enhanced to support agent callbacks or webhooks
            return null;
        }

        private async Task RecordConfigurationUpdateAsync(
            string agentId, 
            AgentConfiguration? previousConfig, 
            AgentConfiguration? newConfig, 
            string changeDescription, 
            bool pushSuccessful, 
            string? errorMessage = null)
        {
            try
            {
                var historyEntry = new ConfigurationUpdateHistory
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "System", // This could be enhanced to track actual users
                    ChangeDescription = changeDescription,
                    PreviousConfiguration = previousConfig,
                    NewConfiguration = newConfig,
                    PushSuccessful = pushSuccessful,
                    ErrorMessage = errorMessage
                };

                _configurationHistory.AddOrUpdate(agentId, 
                    new List<ConfigurationUpdateHistory> { historyEntry },
                    (key, existing) => 
                    {
                        existing.Add(historyEntry);
                        // Keep only last 100 entries per agent
                        if (existing.Count > 100)
                        {
                            existing.RemoveRange(0, existing.Count - 100);
                        }
                        return existing;
                    });

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording configuration update history for agent {AgentId}", agentId);
            }
        }
    }
}
