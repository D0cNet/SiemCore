using SiemAgent.Collectors;
using SiemAgent.Models;
using SiemAgent.Services;

namespace SiemAgent
{
    /// <summary>
    /// Main worker service that orchestrates all SIEM Agent components
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AgentConfiguration _configuration;
        private readonly IEventCacheService _eventCacheService;
        private readonly IEventForwarderService _eventForwarderService;
        private readonly IAgentHealthService _healthService;
        private readonly IConfigurationUpdateService _configurationUpdateService;
        private readonly List<IEventCollector> _collectors;
        private readonly Timer _healthCheckTimer;
        private readonly Timer _configurationRefreshTimer;
        private readonly Timer _cacheFlushTimer;

        public Worker(
            ILogger<Worker> logger,
            AgentConfiguration configuration,
            IEventCacheService eventCacheService,
            IEventForwarderService eventForwarderService,
            IAgentHealthService healthService,
            IConfigurationUpdateService configurationUpdateService,
            IEnumerable<IEventCollector> collectors)
        {
            _logger = logger;
            _configuration = configuration;
            _eventCacheService = eventCacheService;
            _eventForwarderService = eventForwarderService;
            _healthService = healthService;
            _configurationUpdateService = configurationUpdateService;
            _collectors = collectors.ToList();

            // Subscribe to configuration updates
            _configurationUpdateService.ConfigurationUpdated += OnConfigurationUpdated;

            // Initialize timers
            _healthCheckTimer = new Timer(SendHealthCheck, null, Timeout.Infinite, Timeout.Infinite);
            _configurationRefreshTimer = new Timer(RefreshConfiguration, null, Timeout.Infinite, Timeout.Infinite);
            _cacheFlushTimer = new Timer(FlushCachedEvents, null, Timeout.Infinite, Timeout.Infinite);

            // Subscribe to connection status changes
            _eventForwarderService.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SIEM Agent starting up...");

            try
            {
                // Initialize services
                await InitializeServicesAsync();

                // Initialize and start collectors
                await InitializeCollectorsAsync();

                // Start timers
                StartTimers();

                _logger.LogInformation("SIEM Agent started successfully");

                // Keep the service running
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Perform periodic maintenance tasks
                        await PerformMaintenanceAsync();

                        // Wait before next iteration
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in main worker loop");
                        await _healthService.RecordErrorAsync($"Main loop error: {ex.Message}");

                        // Wait before retrying
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Critical error in SIEM Agent");
                await _healthService.RecordErrorAsync($"Critical error: {ex.Message}");
                throw;
            }
            finally
            {
                await ShutdownAsync();
            }
        }

        private async Task InitializeServicesAsync()
        {
            _logger.LogInformation("Initializing services...");

            // Initialize event cache
            if (!await _eventCacheService.InitializeAsync())
            {
                throw new InvalidOperationException("Failed to initialize event cache service");
            }

            // Test connection to SIEM Core
            var connectionTest = await _eventForwarderService.TestConnectionAsync();
            if (!connectionTest)
            {
                _logger.LogWarning("Initial connection test to SIEM Core failed - will retry periodically");
                await _healthService.RecordWarningAsync("Initial connection to SIEM Core failed");
            }

            _logger.LogInformation("Services initialized successfully");
        }

        private async Task InitializeCollectorsAsync()
        {
            _logger.LogInformation("Initializing event collectors...");

            var enabledCollectors = _collectors.Where(c => c.IsEnabled).ToList();
            var initializedCount = 0;

            foreach (var collector in enabledCollectors)
            {
                try
                {
                    // Configure collector
                    var collectorConfig = _configuration.Collectors
                        .FirstOrDefault(c => c.Type == collector.Type);

                    if (collectorConfig != null)
                    {
                        collector.Configuration = collectorConfig;
                    }

                    // Initialize collector
                    if (await collector.InitializeAsync())
                    {
                        // Subscribe to events
                        collector.EventCollected += OnEventCollected;
                        collector.ErrorOccurred += OnCollectorError;

                        // Start collecting
                        _ = Task.Run(() => collector.CollectEventsAsync());

                        initializedCount++;
                        _logger.LogInformation($"Initialized collector: {collector.Name}");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to initialize collector: {collector.Name}");
                        await _healthService.RecordWarningAsync($"Failed to initialize collector: {collector.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error initializing collector: {collector.Name}");
                    await _healthService.RecordErrorAsync($"Collector initialization error ({collector.Name}): {ex.Message}");
                }
            }

            _logger.LogInformation($"Initialized {initializedCount} of {enabledCollectors.Count} collectors");
        }

        private void StartTimers()
        {
            // Start health check timer
            var healthCheckInterval = TimeSpan.FromSeconds(_configuration.HealthCheckIntervalSeconds);
            _healthCheckTimer.Change(healthCheckInterval, healthCheckInterval);

            // Start configuration refresh timer
            var configRefreshInterval = TimeSpan.FromSeconds(_configuration.ConfigurationRefreshIntervalSeconds);
            _configurationRefreshTimer.Change(configRefreshInterval, configRefreshInterval);

            // Start cache flush timer
            var cacheFlushInterval = TimeSpan.FromSeconds(_configuration.EventFlushIntervalSeconds);
            _cacheFlushTimer.Change(cacheFlushInterval, cacheFlushInterval);

            _logger.LogInformation("Started periodic timers");
        }

        private async void OnEventCollected(object? sender, SiemEvent siemEvent)
        {
            try
            {
                await _healthService.UpdateEventStatisticsAsync(1, 0, 0, 0);

                // Try to forward event immediately if connected
                if (_eventForwarderService.IsConnected)
                {
                    var forwarded = await _eventForwarderService.ForwardEventAsync(siemEvent);
                    if (forwarded)
                    {
                        await _healthService.UpdateEventStatisticsAsync(0, 1, 0, 0);
                        return;
                    }
                }

                // Cache event if forwarding failed or not connected
                var cached = await _eventCacheService.CacheEventAsync(siemEvent);
                if (cached)
                {
                    await _healthService.UpdateEventStatisticsAsync(0, 0, 1, 0);
                }
                else
                {
                    _logger.LogWarning($"Failed to cache event {siemEvent.Id}");
                    await _healthService.RecordWarningAsync($"Failed to cache event {siemEvent.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing collected event {siemEvent.Id}");
                await _healthService.RecordErrorAsync($"Event processing error: {ex.Message}");
            }
        }

        private async void OnCollectorError(object? sender, string error)
        {
            _logger.LogError("Collector error: {Error}", error);
            await _healthService.RecordErrorAsync($"Collector error: {error}");
        }

        private void OnConnectionStatusChanged(object? sender, bool isConnected)
        {
            _healthService.SetConnectionStatus(isConnected);

            if (isConnected)
            {
                _logger.LogInformation("Connection to SIEM Core established");
                // Trigger immediate cache flush when connection is restored
                _ = Task.Run(FlushCachedEventsAsync);
            }
            else
            {
                _logger.LogWarning("Connection to SIEM Core lost");
            }
        }

        private void OnConfigurationUpdated(object? sender, ConfigurationUpdatedEventArgs e)
        {
            _logger.LogInformation("Configuration updated from {Source} at {UpdatedAt}",
                e.UpdateSource, e.UpdatedAt);

            if (e.RestartRequired)
            {
                _logger.LogWarning("Configuration changes require agent restart. Consider restarting the service.");
            }

            // Update health service with new configuration timestamp
            _healthService.SetConfigurationUpdateTime(e.UpdatedAt);

            // Log configuration changes
            if (e.PreviousConfiguration != null)
            {
                _logger.LogDebug("Configuration updated: EventBatchSize {OldBatch} -> {NewBatch}, LogLevel {OldLevel} -> {NewLevel}",
                    e.PreviousConfiguration.EventBatchSize, e.NewConfiguration.EventBatchSize,
                    e.PreviousConfiguration.LogLevel, e.NewConfiguration.LogLevel);
            }
        }

        private async void SendHealthCheck(object? state)
        {
            try
            {
                var healthStatus = await _healthService.GetHealthStatusAsync();

                if (_eventForwarderService.IsConnected)
                {
                    await _eventForwarderService.SendHealthStatusAsync(healthStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending health check");
            }
        }

        private async void RefreshConfiguration(object? state)
        {
            try
            {
                if (_eventForwarderService.IsConnected)
                {
                    var newConfiguration = await _eventForwarderService.GetConfigurationAsync();
                    if (newConfiguration != null)
                    {
                        // Apply new configuration through the configuration update service
                        var applied = await _configurationUpdateService.ApplyConfigurationAsync(newConfiguration);
                        if (applied)
                        {
                            _logger.LogInformation("Configuration refreshed and applied successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Configuration refresh failed to apply new settings");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing configuration");
            }
        }

        private async void FlushCachedEvents(object? state)
        {
            await FlushCachedEventsAsync();
        }

        private async Task FlushCachedEventsAsync()
        {
            try
            {
                if (!_eventForwarderService.IsConnected)
                    return;

                var cachedEvents = await _eventCacheService.GetCachedEventsAsync(_configuration.EventBatchSize);
                var eventList = cachedEvents.ToList();

                if (!eventList.Any())
                    return;

                var forwarded = await _eventForwarderService.ForwardEventsAsync(eventList);
                if (forwarded)
                {
                    var eventIds = eventList.Select(e => e.Id);
                    await _eventCacheService.RemoveCachedEventsAsync(eventIds);
                    await _healthService.UpdateEventStatisticsAsync(0, eventList.Count, -eventList.Count, 0);

                    _logger.LogInformation($"Flushed {eventList.Count} cached events");
                }
                else
                {
                    // Increment retry count for failed events
                    foreach (var evt in eventList)
                    {
                        evt.RetryCount++;
                        if (evt.RetryCount <= _configuration.MaxRetryAttempts)
                        {
                            await _eventCacheService.CacheEventAsync(evt);
                        }
                        else
                        {
                            _logger.LogWarning($"Dropping event {evt.Id} after {evt.RetryCount} retry attempts");
                            await _eventCacheService.RemoveCachedEventAsync(evt.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing cached events");
                await _healthService.RecordErrorAsync($"Cache flush error: {ex.Message}");
            }
        }

        private async Task PerformMaintenanceAsync()
        {
            try
            {
                // Cleanup expired cached events (older than 7 days)
                await _eventCacheService.CleanupExpiredEventsAsync(TimeSpan.FromDays(7));

                // Check cache size and warn if it's getting too large
                var cachedCount = await _eventCacheService.GetCachedEventCountAsync();
                if (cachedCount > _configuration.MaxCachedEvents * 0.8)
                {
                    await _healthService.RecordWarningAsync($"Event cache is {cachedCount}/{_configuration.MaxCachedEvents} events");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance");
            }
        }

        private async Task ShutdownAsync()
        {
            _logger.LogInformation("SIEM Agent shutting down...");

            try
            {
                // Stop timers
                await _healthCheckTimer.DisposeAsync();
                await _configurationRefreshTimer.DisposeAsync();
                await _cacheFlushTimer.DisposeAsync();

                // Stop collectors
                foreach (var collector in _collectors)
                {
                    try
                    {
                        await collector.StopAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error stopping collector: {collector.Name}");
                    }
                }

                // Final flush of cached events
                await FlushCachedEventsAsync();

                _logger.LogInformation("SIEM Agent shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during shutdown");
            }
        }

        public override void Dispose()
        {
            _healthCheckTimer?.Dispose();
            _configurationRefreshTimer?.Dispose();
            _cacheFlushTimer?.Dispose();
            base.Dispose();
        }
    }
}
