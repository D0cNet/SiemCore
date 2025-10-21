using SiemAgent.Models;
using System.Diagnostics;

namespace SiemAgent.Services
{
    /// <summary>
    /// Implementation of agent health monitoring service
    /// </summary>
    public class AgentHealthService : IAgentHealthService
    {
        private readonly ILogger<AgentHealthService> _logger;
        private readonly AgentConfiguration _configuration;
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly object _lockObject = new object();

        private long _eventsCollected = 0;
        private long _eventsForwarded = 0;
        private long _eventsCached = 0;
        private long _eventsFiltered = 0;
        private bool _isConnectedToSiemCore = false;
        private DateTime? _lastSuccessfulConnection;
        private DateTime? _lastConfigurationUpdate;

        public DateTime StartTime { get; } = DateTime.UtcNow;

        public AgentHealthService(ILogger<AgentHealthService> logger, AgentConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AgentHealth> GetHealthStatusAsync()
        {
            var health = new AgentHealth
            {
                AgentId = _configuration.AgentId,
                AgentVersion = _configuration.AgentVersion,
                Timestamp = DateTime.UtcNow,
                StartTime = StartTime,
                EventsCollected = _eventsCollected,
                EventsForwarded = _eventsForwarded,
                EventsCached = _eventsCached,
                EventsFiltered = _eventsFiltered,
                IsConnectedToSiemCore = _isConnectedToSiemCore,
                LastSuccessfulConnection = _lastSuccessfulConnection,
                LastConfigurationUpdate = _lastConfigurationUpdate,
                CpuUsagePercent = await GetCpuUsageAsync(),
                MemoryUsageBytes = await GetMemoryUsageAsync(),
                DiskUsageBytes = await GetDiskUsageAsync()
            };

            // Determine overall status
            health.Status = DetermineAgentStatus(health);
            health.StatusMessage = GetStatusMessage(health.Status);

            // Copy errors and warnings
            lock (_lockObject)
            {
                health.Errors = new List<string>(_errors);
                health.Warnings = new List<string>(_warnings);
            }

            return health;
        }

        public async Task UpdateEventStatisticsAsync(long collected, long forwarded, long cached, long filtered)
        {
            Interlocked.Add(ref _eventsCollected, collected);
            Interlocked.Add(ref _eventsForwarded, forwarded);
            Interlocked.Add(ref _eventsCached, cached);
            Interlocked.Add(ref _eventsFiltered, filtered);

            await Task.CompletedTask;
        }

        public async Task RecordErrorAsync(string error)
        {
            lock (_lockObject)
            {
                _errors.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {error}");
                
                // Keep only the last 50 errors
                if (_errors.Count > 50)
                {
                    _errors.RemoveAt(0);
                }
            }

            _logger.LogError("Agent error recorded: {Error}", error);
            await Task.CompletedTask;
        }

        public async Task RecordWarningAsync(string warning)
        {
            lock (_lockObject)
            {
                _warnings.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {warning}");
                
                // Keep only the last 50 warnings
                if (_warnings.Count > 50)
                {
                    _warnings.RemoveAt(0);
                }
            }

            _logger.LogWarning("Agent warning recorded: {Warning}", warning);
            await Task.CompletedTask;
        }

        public async Task ClearErrorsAndWarningsAsync()
        {
            lock (_lockObject)
            {
                _errors.Clear();
                _warnings.Clear();
            }

            _logger.LogInformation("Cleared all errors and warnings");
            await Task.CompletedTask;
        }

        public async Task<double> GetCpuUsageAsync()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;
                
                await Task.Delay(1000); // Wait 1 second
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                return Math.Round(cpuUsageTotal * 100, 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get CPU usage");
                return 0.0;
            }
        }

        public async Task<long> GetMemoryUsageAsync()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                await Task.CompletedTask;
                return process.WorkingSet64;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get memory usage");
                return 0;
            }
        }

        public async Task<long> GetDiskUsageAsync()
        {
            try
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var directoryInfo = new DirectoryInfo(currentDirectory);
                
                await Task.CompletedTask;
                return GetDirectorySize(directoryInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get disk usage");
                return 0;
            }
        }

        public void SetConnectionStatus(bool isConnected)
        {
            _isConnectedToSiemCore = isConnected;
            
            if (isConnected)
            {
                _lastSuccessfulConnection = DateTime.UtcNow;
            }
        }

        public void SetConfigurationUpdateTime(DateTime updateTime)
        {
            _lastConfigurationUpdate = updateTime;
        }

        private AgentStatus DetermineAgentStatus(AgentHealth health)
        {
            // Check for errors
            if (health.Errors.Any())
            {
                return AgentStatus.Error;
            }

            // Check connection status
            if (!health.IsConnectedToSiemCore)
            {
                return AgentStatus.Warning;
            }

            // Check if configuration is outdated (more than 1 hour)
            if (health.LastConfigurationUpdate.HasValue &&
                DateTime.UtcNow - health.LastConfigurationUpdate.Value > TimeSpan.FromHours(1))
            {
                return AgentStatus.Warning;
            }

            // Check resource usage
            if (health.CpuUsagePercent > 80 || health.MemoryUsageBytes > 1024 * 1024 * 1024) // 1GB
            {
                return AgentStatus.Warning;
            }

            // Check for warnings
            if (health.Warnings.Any())
            {
                return AgentStatus.Warning;
            }

            return AgentStatus.Running;
        }

        private string GetStatusMessage(AgentStatus status)
        {
            return status switch
            {
                AgentStatus.Starting => "Agent is starting up",
                AgentStatus.Running => "Agent is running normally",
                AgentStatus.Warning => "Agent is running with warnings",
                AgentStatus.Error => "Agent has encountered errors",
                AgentStatus.Stopping => "Agent is shutting down",
                AgentStatus.Stopped => "Agent is stopped",
                _ => "Unknown status"
            };
        }

        private long GetDirectorySize(DirectoryInfo directoryInfo)
        {
            long size = 0;

            try
            {
                // Get size of all files in the directory
                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    size += fileInfo.Length;
                }

                // Get size of all subdirectories
                foreach (var subDirectory in directoryInfo.GetDirectories())
                {
                    size += GetDirectorySize(subDirectory);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (DirectoryNotFoundException)
            {
                // Skip directories that don't exist
            }

            return size;
        }
    }
}
