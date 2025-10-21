using SiemAgent.Models;
using System.Text.Json;

namespace SiemAgent.Services
{
    /// <summary>
    /// Service for handling configuration updates from SiemCore
    /// </summary>
    public class ConfigurationUpdateService : IConfigurationUpdateService
    {
        private readonly ILogger<ConfigurationUpdateService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _configurationFilePath;
        private readonly string _backupConfigurationPath;
        private AgentConfiguration? _currentConfiguration;
        private readonly JsonSerializerOptions _jsonOptions;

        public event EventHandler<ConfigurationUpdatedEventArgs>? ConfigurationUpdated;

        public ConfigurationUpdateService(
            ILogger<ConfigurationUpdateService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Determine configuration file path
            _configurationFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _backupConfigurationPath = Path.Combine(AppContext.BaseDirectory, "appsettings.backup.json");
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            LoadCurrentConfiguration();
        }

        public async Task<bool> ApplyConfigurationAsync(AgentConfiguration newConfiguration)
        {
            try
            {
                _logger.LogInformation("Applying new configuration for agent {AgentId}", newConfiguration.AgentId);

                // Validate the new configuration
                var validationResult = await ValidateConfigurationAsync(newConfiguration);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Configuration validation failed: {Errors}", 
                        string.Join(", ", validationResult.Errors));
                    return false;
                }

                // Backup current configuration
                var backupSuccess = await BackupCurrentConfigurationAsync();
                if (!backupSuccess)
                {
                    _logger.LogWarning("Failed to backup current configuration, proceeding anyway");
                }

                var previousConfiguration = _currentConfiguration;

                try
                {
                    // Update the configuration file
                    await UpdateConfigurationFileAsync(newConfiguration);

                    // Update in-memory configuration
                    _currentConfiguration = newConfiguration;

                    // Fire configuration updated event
                    var eventArgs = new ConfigurationUpdatedEventArgs
                    {
                        PreviousConfiguration = previousConfiguration,
                        NewConfiguration = newConfiguration,
                        UpdatedAt = DateTime.UtcNow,
                        UpdateSource = "SiemCore Push",
                        RestartRequired = validationResult.RestartRequired
                    };

                    ConfigurationUpdated?.Invoke(this, eventArgs);

                    _logger.LogInformation("Configuration applied successfully for agent {AgentId}. Restart required: {RestartRequired}", 
                        newConfiguration.AgentId, validationResult.RestartRequired);

                    if (validationResult.RestartRequired)
                    {
                        _logger.LogWarning("Configuration changes require agent restart to take full effect");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying configuration, attempting to restore previous configuration");
                    
                    // Attempt to restore previous configuration
                    await RestorePreviousConfigurationAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying configuration for agent {AgentId}", newConfiguration.AgentId);
                return false;
            }
        }

        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(AgentConfiguration configuration)
        {
            var result = new ConfigurationValidationResult { IsValid = true };

            try
            {
                // Validate required fields
                if (string.IsNullOrEmpty(configuration.AgentId))
                {
                    result.Errors.Add("AgentId is required");
                }

                if (string.IsNullOrEmpty(configuration.AgentVersion))
                {
                    result.Errors.Add("AgentVersion is required");
                }

                if (string.IsNullOrEmpty(configuration.SiemCoreApiUrl))
                {
                    result.Errors.Add("SiemCoreApiUrl is required");
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
                if (!validLogLevels.Any(level => level.Equals(configuration.LogLevel, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Errors.Add($"LogLevel must be one of: {string.Join(", ", validLogLevels)}");
                }

                // Check for changes that require restart
                if (_currentConfiguration != null)
                {
                    if (_currentConfiguration.SiemCoreApiUrl != configuration.SiemCoreApiUrl ||
                        _currentConfiguration.ApiKey != configuration.ApiKey ||
                        _currentConfiguration.HealthCheckIntervalSeconds != configuration.HealthCheckIntervalSeconds ||
                        _currentConfiguration.ConfigurationRefreshIntervalSeconds != configuration.ConfigurationRefreshIntervalSeconds)
                    {
                        result.RestartRequired = true;
                        result.Warnings.Add("Some configuration changes require agent restart to take effect");
                    }
                }

                // Add performance warnings
                if (configuration.EventBatchSize > 1000)
                {
                    result.Warnings.Add("Large EventBatchSize may impact memory usage");
                }

                if (configuration.EventFlushIntervalSeconds > 300)
                {
                    result.Warnings.Add("Long EventFlushInterval may delay event processing");
                }

                if (configuration.MaxCachedEvents > 100000)
                {
                    result.Warnings.Add("Large MaxCachedEvents may impact memory usage");
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

        public async Task<AgentConfiguration> GetCurrentConfigurationAsync()
        {
            if (_currentConfiguration == null)
            {
                LoadCurrentConfiguration();
            }

            return await Task.FromResult(_currentConfiguration ?? new AgentConfiguration());
        }

        public async Task<bool> BackupCurrentConfigurationAsync()
        {
            try
            {
                if (File.Exists(_configurationFilePath))
                {
                    await File.WriteAllTextAsync(_backupConfigurationPath, 
                        await File.ReadAllTextAsync(_configurationFilePath));
                    
                    _logger.LogDebug("Configuration backed up to {BackupPath}", _backupConfigurationPath);
                    return true;
                }
                
                _logger.LogWarning("Configuration file not found for backup: {ConfigPath}", _configurationFilePath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up configuration");
                return false;
            }
        }

        public async Task<bool> RestorePreviousConfigurationAsync()
        {
            try
            {
                if (File.Exists(_backupConfigurationPath))
                {
                    await File.WriteAllTextAsync(_configurationFilePath, 
                        await File.ReadAllTextAsync(_backupConfigurationPath));
                    
                    LoadCurrentConfiguration();
                    
                    _logger.LogInformation("Previous configuration restored from backup");
                    return true;
                }
                
                _logger.LogWarning("Backup configuration file not found: {BackupPath}", _backupConfigurationPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring previous configuration");
                return false;
            }
        }

        private void LoadCurrentConfiguration()
        {
            try
            {
                var agentConfig = new AgentConfiguration
                {
                    AgentId = _configuration["Agent:AgentId"] ?? "",
                    AgentVersion = _configuration["Agent:AgentVersion"] ?? "1.0.0",
                    EventBatchSize = _configuration.GetValue<int>("Agent:EventBatchSize", 100),
                    EventFlushIntervalSeconds = _configuration.GetValue<int>("Agent:EventFlushIntervalSeconds", 30),
                    MaxRetryAttempts = _configuration.GetValue<int>("Agent:MaxRetryAttempts", 3),
                    RetryDelaySeconds = _configuration.GetValue<int>("Agent:RetryDelaySeconds", 5),
                    MaxCachedEvents = _configuration.GetValue<int>("Agent:MaxCachedEvents", 10000),
                    EnableLocalAnalysis = _configuration.GetValue<bool>("Agent:EnableLocalAnalysis", true),
                    EnableEventFiltering = _configuration.GetValue<bool>("Agent:EnableEventFiltering", true),
                    LogLevel = _configuration["Agent:LogLevel"] ?? "Information",
                    HealthCheckIntervalSeconds = _configuration.GetValue<int>("Agent:HealthCheckIntervalSeconds", 60),
                    ConfigurationRefreshIntervalSeconds = _configuration.GetValue<int>("Agent:ConfigurationRefreshIntervalSeconds", 300),
                    SiemCoreApiUrl = _configuration["SiemCore:ApiUrl"] ?? "",
                    ApiKey = _configuration["SiemCore:ApiKey"] ?? ""
                };

                _currentConfiguration = agentConfig;
                _logger.LogDebug("Current configuration loaded for agent {AgentId}", agentConfig.AgentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading current configuration");
                _currentConfiguration = new AgentConfiguration();
            }
        }

        private async Task UpdateConfigurationFileAsync(AgentConfiguration newConfiguration)
        {
            try
            {
                // Read the current configuration file
                var configJson = await File.ReadAllTextAsync(_configurationFilePath);
                var configDocument = JsonDocument.Parse(configJson);
                var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson);

                if (configDict == null)
                {
                    throw new InvalidOperationException("Failed to parse configuration file");
                }

                // Update the relevant sections
                if (!configDict.ContainsKey("SiemCore"))
                {
                    configDict["SiemCore"] = new Dictionary<string, object>();
                }

                if (!configDict.ContainsKey("Agent"))
                {
                    configDict["Agent"] = new Dictionary<string, object>();
                }

                var siemCoreSection = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(configDict["SiemCore"])) ?? new Dictionary<string, object>();

                var agentSection = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(configDict["Agent"])) ?? new Dictionary<string, object>();

                // Update SiemCore section
                siemCoreSection["ApiUrl"] = newConfiguration.SiemCoreApiUrl;
                siemCoreSection["ApiKey"] = newConfiguration.ApiKey;

                // Update Agent section
                agentSection["AgentId"] = newConfiguration.AgentId;
                agentSection["AgentVersion"] = newConfiguration.AgentVersion;
                agentSection["EventBatchSize"] = newConfiguration.EventBatchSize;
                agentSection["EventFlushIntervalSeconds"] = newConfiguration.EventFlushIntervalSeconds;
                agentSection["MaxRetryAttempts"] = newConfiguration.MaxRetryAttempts;
                agentSection["RetryDelaySeconds"] = newConfiguration.RetryDelaySeconds;
                agentSection["MaxCachedEvents"] = newConfiguration.MaxCachedEvents;
                agentSection["EnableLocalAnalysis"] = newConfiguration.EnableLocalAnalysis;
                agentSection["EnableEventFiltering"] = newConfiguration.EnableEventFiltering;
                agentSection["LogLevel"] = newConfiguration.LogLevel;
                agentSection["HealthCheckIntervalSeconds"] = newConfiguration.HealthCheckIntervalSeconds;
                agentSection["ConfigurationRefreshIntervalSeconds"] = newConfiguration.ConfigurationRefreshIntervalSeconds;

                configDict["SiemCore"] = siemCoreSection;
                configDict["Agent"] = agentSection;

                // Write the updated configuration back to file
                var updatedJson = JsonSerializer.Serialize(configDict, _jsonOptions);
                await File.WriteAllTextAsync(_configurationFilePath, updatedJson);

                _logger.LogDebug("Configuration file updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration file");
                throw;
            }
        }
    }
}
