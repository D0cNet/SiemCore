using SiemAgent.Models;
using System.Text.RegularExpressions;

namespace SiemAgent.Collectors
{
    /// <summary>
    /// Collector for monitoring log files
    /// </summary>
    public class FileLogCollector : IEventCollector
    {
        private readonly ILogger<FileLogCollector> _logger;
        private readonly List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();
        private readonly Dictionary<string, long> _filePositions = new Dictionary<string, long>();
        private bool _isRunning = false;
        private DateTime _lastCollectionTime = DateTime.UtcNow;

        public string Name => "File Log Collector";
        public string Type => "FileLog";
        public bool IsEnabled { get; set; } = true;
        public CollectorConfiguration Configuration { get; set; } = new CollectorConfiguration();

        public event EventHandler<SiemEvent>? EventCollected;
        public event EventHandler<string>? ErrorOccurred;

        public FileLogCollector(ILogger<FileLogCollector> logger)
        {
            _logger = logger;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                var logPaths = Configuration.Settings.GetValueOrDefault("LogPaths", new List<string>()) as List<string> 
                              ?? new List<string>();

                if (!logPaths.Any())
                {
                    _logger.LogWarning("No log paths configured for File Log Collector");
                    return false;
                }

                foreach (var logPath in logPaths)
                {
                    if (File.Exists(logPath))
                    {
                        SetupFileWatcher(logPath);
                        // Initialize file position to end of file for new monitoring
                        _filePositions[logPath] = new FileInfo(logPath).Length;
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(logPath)))
                    {
                        SetupDirectoryWatcher(logPath);
                    }
                    else
                    {
                        _logger.LogWarning($"Log path does not exist: {logPath}");
                    }
                }

                _logger.LogInformation($"File Log Collector initialized for {logPaths.Count} paths");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize File Log Collector");
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task<IEnumerable<SiemEvent>> CollectEventsAsync()
        {
            var events = new List<SiemEvent>();

            try
            {
                if (!_isRunning)
                {
                    foreach (var watcher in _fileWatchers)
                    {
                        watcher.EnableRaisingEvents = true;
                    }
                    _isRunning = true;
                    _logger.LogInformation("Started file monitoring");
                }

                // Also perform periodic scan of existing files
                await ScanExistingFilesAsync(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting file log events");
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
                StatusMessage = $"Monitoring {_fileWatchers.Count} file paths",
                LastCollection = _lastCollectionTime
            };
        }

        public async Task StopAsync()
        {
            try
            {
                foreach (var watcher in _fileWatchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _fileWatchers.Clear();
                _isRunning = false;

                _logger.LogInformation("File Log Collector stopped");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping File Log Collector");
            }
        }

        private void SetupFileWatcher(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                return;

            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = false
            };

            watcher.Changed += (sender, e) => OnFileChanged(e.FullPath);
            watcher.Error += (sender, e) => OnWatcherError(e.GetException());

            _fileWatchers.Add(watcher);
        }

        private void SetupDirectoryWatcher(string pattern)
        {
            var directory = Path.GetDirectoryName(pattern);
            var filePattern = Path.GetFileName(pattern);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(filePattern))
                return;

            var watcher = new FileSystemWatcher(directory, filePattern)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = false
            };

            watcher.Changed += (sender, e) => OnFileChanged(e.FullPath);
            watcher.Created += (sender, e) => OnFileCreated(e.FullPath);
            watcher.Error += (sender, e) => OnWatcherError(e.GetException());

            _fileWatchers.Add(watcher);
        }

        private async Task ScanExistingFilesAsync(List<SiemEvent> events)
        {
            var logPaths = Configuration.Settings.GetValueOrDefault("LogPaths", new List<string>()) as List<string>
                          ?? new List<string>();

            foreach (var logPath in logPaths)
            {
                if (File.Exists(logPath))
                {
                    await ProcessFileChanges(logPath);
                }
            }
        }

        private void OnFileCreated(string filePath)
        {
            _logger.LogDebug($"New file created: {filePath}");
            _filePositions[filePath] = 0;
            _ = Task.Run(() => ProcessFileChanges(filePath));
        }

        private void OnFileChanged(string filePath)
        {
            _logger.LogDebug($"File changed: {filePath}");
            _ = Task.Run(() => ProcessFileChanges(filePath));
        }

        private void OnWatcherError(Exception exception)
        {
            _logger.LogError(exception, "File watcher error occurred");
            ErrorOccurred?.Invoke(this, exception.Message);
        }

        private async Task ProcessFileChanges(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var fileInfo = new FileInfo(filePath);
                var currentPosition = _filePositions.GetValueOrDefault(filePath, 0);

                if (fileInfo.Length <= currentPosition)
                    return; // No new content

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fileStream.Seek(currentPosition, SeekOrigin.Begin);

                using var reader = new StreamReader(fileStream);
                string? line;
                var newPosition = currentPosition;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var siemEvent = ParseLogLine(line, filePath);
                        if (siemEvent != null && ShouldIncludeEvent(siemEvent))
                        {
                            _lastCollectionTime = DateTime.UtcNow;
                            EventCollected?.Invoke(this, siemEvent);
                        }
                    }
                    newPosition = fileStream.Position;
                }

                _filePositions[filePath] = newPosition;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing file changes for {filePath}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private SiemEvent? ParseLogLine(string logLine, string filePath)
        {
            try
            {
                var siemEvent = new SiemEvent
                {
                    Timestamp = DateTime.UtcNow,
                    SourceSystem = Environment.MachineName,
                    EventType = "FileLog",
                    Severity = "Low",
                    Description = logLine.Length > 500 ? logLine.Substring(0, 500) + "..." : logLine,
                    RawLog = logLine,
                    CustomFields = new Dictionary<string, object>
                    {
                        ["FilePath"] = filePath,
                        ["FileName"] = Path.GetFileName(filePath)
                    }
                };

                // Try to extract timestamp from log line
                ExtractTimestamp(logLine, siemEvent);

                // Try to extract severity from log line
                ExtractSeverity(logLine, siemEvent);

                // Try to extract IP addresses
                ExtractIpAddresses(logLine, siemEvent);

                return siemEvent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to parse log line: {logLine}");
                return null;
            }
        }

        private void ExtractTimestamp(string logLine, SiemEvent siemEvent)
        {
            // Common timestamp patterns
            var timestampPatterns = new[]
            {
                @"\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}",  // 2024-01-01 12:00:00
                @"\d{2}/\d{2}/\d{4}\s\d{2}:\d{2}:\d{2}",  // 01/01/2024 12:00:00
                @"\w{3}\s+\d{1,2}\s\d{2}:\d{2}:\d{2}"     // Jan 1 12:00:00
            };

            foreach (var pattern in timestampPatterns)
            {
                var match = Regex.Match(logLine, pattern);
                if (match.Success && DateTime.TryParse(match.Value, out var timestamp))
                {
                    siemEvent.Timestamp = timestamp;
                    break;
                }
            }
        }

        private void ExtractSeverity(string logLine, SiemEvent siemEvent)
        {
            var upperLine = logLine.ToUpper();
            
            if (upperLine.Contains("ERROR") || upperLine.Contains("FATAL"))
                siemEvent.Severity = "High";
            else if (upperLine.Contains("WARN"))
                siemEvent.Severity = "Medium";
            else if (upperLine.Contains("INFO"))
                siemEvent.Severity = "Low";
            else if (upperLine.Contains("DEBUG") || upperLine.Contains("TRACE"))
                siemEvent.Severity = "Low";
        }

        private void ExtractIpAddresses(string logLine, SiemEvent siemEvent)
        {
            var ipPattern = @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b";
            var matches = Regex.Matches(logLine, ipPattern);

            if (matches.Count > 0)
            {
                siemEvent.SourceIp = matches[0].Value;
                if (matches.Count > 1)
                {
                    siemEvent.DestinationIp = matches[1].Value;
                }
            }
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
            _fileWatchers.Clear();
            IsEnabled = false;
        }
    }
}
