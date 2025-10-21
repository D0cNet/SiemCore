using SiemAgent.Models;
using System.Diagnostics.Eventing.Reader;

namespace SiemAgent.Collectors
{
    /// <summary>
    /// Collector for Windows Event Logs
    /// </summary>
    public class WindowsEventLogCollector : IEventCollector
    {
        private readonly ILogger<WindowsEventLogCollector> _logger;
        private EventLogWatcher? _eventLogWatcher;
        private bool _isRunning = false;
        private DateTime _lastCollectionTime = DateTime.UtcNow;

        public string Name => "Windows Event Log Collector";
        public string Type => "WindowsEventLog";
        public bool IsEnabled { get; set; } = true;
        public CollectorConfiguration Configuration { get; set; } = new CollectorConfiguration();

        public event EventHandler<SiemEvent>? EventCollected;
        public event EventHandler<string>? ErrorOccurred;

        public WindowsEventLogCollector(ILogger<WindowsEventLogCollector> logger)
        {
            _logger = logger;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogWarning("Windows Event Log Collector is only supported on Windows");
                    return false;
                }

                var logName = Configuration.Settings.GetValueOrDefault("LogName", "Security")?.ToString() ?? "Security";
                var query = Configuration.Settings.GetValueOrDefault("Query", "*")?.ToString() ?? "*";

                var eventQuery = new EventLogQuery(logName, PathType.LogName, query);
                _eventLogWatcher = new EventLogWatcher(eventQuery);
                _eventLogWatcher.EventRecordWritten += OnEventRecordWritten;

                _logger.LogInformation($"Windows Event Log Collector initialized for log: {logName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Windows Event Log Collector");
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task<IEnumerable<SiemEvent>> CollectEventsAsync()
        {
            var events = new List<SiemEvent>();
            
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    return events;
                }

                if (!_isRunning && _eventLogWatcher != null)
                {
                    _eventLogWatcher.Enabled = true;
                    _isRunning = true;
                    _logger.LogInformation("Started Windows Event Log monitoring");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting Windows Event Log events");
                ErrorOccurred?.Invoke(this, ex.Message);
            }

            return events;
        }

        public async Task<CollectorHealth> GetHealthStatusAsync()
        {
            await Task.CompletedTask;
            
            return new CollectorHealth
            {
                Name = Name,
                Type = Type,
                Status = _isRunning ? AgentStatus.Running : AgentStatus.Stopped,
                StatusMessage = _isRunning ? "Monitoring Windows Event Logs" : "Not running",
                LastCollection = _lastCollectionTime
            };
        }

        public async Task StopAsync()
        {
            try
            {
                if (_eventLogWatcher != null)
                {
                    _eventLogWatcher.Enabled = false;
                    _eventLogWatcher.EventRecordWritten -= OnEventRecordWritten;
                    _eventLogWatcher.Dispose();
                    _eventLogWatcher = null;
                }

                _isRunning = false;
                _logger.LogInformation("Windows Event Log Collector stopped");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Windows Event Log Collector");
            }
        }

        private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                if (e.EventRecord == null) return;

                var eventRecord = e.EventRecord;
                var siemEvent = new SiemEvent
                {
                    Timestamp = eventRecord.TimeCreated ?? DateTime.UtcNow,
                    SourceSystem = Environment.MachineName,
                    EventType = "WindowsEvent",
                    Severity = MapEventLevelToSeverity(eventRecord.Level),
                    Description = eventRecord.FormatDescription() ?? "Windows Event",
                    RawLog = eventRecord.ToXml(),
                    CustomFields = new Dictionary<string, object>
                    {
                        ["EventId"] = eventRecord.Id,
                        ["LogName"] = eventRecord.LogName ?? string.Empty,
                        ["Source"] = eventRecord.ProviderName ?? string.Empty,
                        ["Level"] = eventRecord.Level ?? 0,
                        ["Task"] = eventRecord.Task ?? 0,
                        ["Keywords"] = eventRecord.Keywords ?? 0
                    }
                };

                // Apply filtering if configured
                if (ShouldIncludeEvent(siemEvent))
                {
                    _lastCollectionTime = DateTime.UtcNow;
                    EventCollected?.Invoke(this, siemEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Windows Event Log record");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private string MapEventLevelToSeverity(byte? level)
        {
            return level switch
            {
                1 => "Critical",  // Critical
                2 => "High",      // Error
                3 => "Medium",    // Warning
                4 => "Low",       // Information
                5 => "Low",       // Verbose
                _ => "Medium"
            };
        }

        private bool ShouldIncludeEvent(SiemEvent siemEvent)
        {
            // Apply severity filter
            if (!string.IsNullOrEmpty(Configuration.SeverityFilter))
            {
                if (!siemEvent.Severity.Equals(Configuration.SeverityFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Apply include patterns
            if (Configuration.IncludePatterns.Any())
            {
                var includeMatch = Configuration.IncludePatterns.Any(pattern =>
                    siemEvent.Description.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    siemEvent.RawLog.Contains(pattern, StringComparison.OrdinalIgnoreCase));
                
                if (!includeMatch) return false;
            }

            // Apply exclude patterns
            if (Configuration.ExcludePatterns.Any())
            {
                var excludeMatch = Configuration.ExcludePatterns.Any(pattern =>
                    siemEvent.Description.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    siemEvent.RawLog.Contains(pattern, StringComparison.OrdinalIgnoreCase));
                
                if (excludeMatch) return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (_eventLogWatcher != null)
            {
                _eventLogWatcher.Enabled = false;
                _eventLogWatcher.EventRecordWritten -= OnEventRecordWritten;
                _eventLogWatcher.Dispose();
                _eventLogWatcher = null;
            }
        }
    }
}
