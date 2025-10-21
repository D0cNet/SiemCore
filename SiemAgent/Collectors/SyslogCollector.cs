using SiemAgent.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace SiemAgent.Collectors
{
    /// <summary>
    /// Collector for Syslog messages (UDP and TCP)
    /// </summary>
    public class SyslogCollector : IEventCollector
    {
        private readonly ILogger<SyslogCollector> _logger;
        private UdpClient? _udpClient;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;
        private DateTime _lastCollectionTime = DateTime.UtcNow;

        public string Name => "Syslog Collector";
        public string Type => "Syslog";
        public bool IsEnabled { get; set; } = true;
        public CollectorConfiguration Configuration { get; set; } = new CollectorConfiguration();

        public event EventHandler<SiemEvent>? EventCollected;
        public event EventHandler<string>? ErrorOccurred;

        public SyslogCollector(ILogger<SyslogCollector> logger)
        {
            _logger = logger;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                var port = Convert.ToInt32(Configuration.Settings.GetValueOrDefault("Port", 514));
                var protocol = Configuration.Settings.GetValueOrDefault("Protocol", "UDP")?.ToString() ?? "UDP";

                _cancellationTokenSource = new CancellationTokenSource();

                if (protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase))
                {
                    _udpClient = new UdpClient(port);
                    _logger.LogInformation($"Syslog Collector initialized for UDP on port {port}");
                }
                else if (protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    _tcpListener = new TcpListener(IPAddress.Any, port);
                    _logger.LogInformation($"Syslog Collector initialized for TCP on port {port}");
                }
                else
                {
                    _logger.LogError($"Unsupported protocol: {protocol}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Syslog Collector");
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
                    if (_udpClient != null)
                    {
                        _ = Task.Run(() => StartUdpListening(_cancellationTokenSource!.Token));
                    }
                    else if (_tcpListener != null)
                    {
                        _ = Task.Run(() => StartTcpListening(_cancellationTokenSource!.Token));
                    }

                    _isRunning = true;
                    _logger.LogInformation("Started Syslog listening");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Syslog collection");
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
                StatusMessage = _isRunning ? "Listening for Syslog messages" : "Not running",
                LastCollection = _lastCollectionTime
            };
        }

        public async Task StopAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;

                _tcpListener?.Stop();
                _tcpListener = null;

                _isRunning = false;
                _logger.LogInformation("Syslog Collector stopped");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Syslog Collector");
            }
        }

        private async Task StartUdpListening(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync();
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    var sourceEndpoint = result.RemoteEndPoint;

                    var siemEvent = ParseSyslogMessage(message, sourceEndpoint);
                    if (siemEvent != null && ShouldIncludeEvent(siemEvent))
                    {
                        _lastCollectionTime = DateTime.UtcNow;
                        EventCollected?.Invoke(this, siemEvent);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UDP Syslog listening");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private async Task StartTcpListening(CancellationToken cancellationToken)
        {
            try
            {
                _tcpListener?.Start();

                while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleTcpClient(tcpClient, cancellationToken));
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TCP Syslog listening");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private async Task HandleTcpClient(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            try
            {
                using (tcpClient)
                {
                    var stream = tcpClient.GetStream();
                    var buffer = new byte[4096];

                    while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0) break;

                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        var sourceEndpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;

                        var siemEvent = ParseSyslogMessage(message, sourceEndpoint);
                        if (siemEvent != null && ShouldIncludeEvent(siemEvent))
                        {
                            _lastCollectionTime = DateTime.UtcNow;
                            EventCollected?.Invoke(this, siemEvent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling TCP Syslog client");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private SiemEvent? ParseSyslogMessage(string message, IPEndPoint? sourceEndpoint)
        {
            try
            {
                var siemEvent = new SiemEvent
                {
                    Timestamp = DateTime.UtcNow,
                    SourceSystem = sourceEndpoint?.Address.ToString() ?? "Unknown",
                    EventType = "Syslog",
                    Severity = "Low",
                    Description = message.Length > 500 ? message.Substring(0, 500) + "..." : message,
                    SourceIp = sourceEndpoint?.Address.ToString() ?? string.Empty,
                    RawLog = message,
                    CustomFields = new Dictionary<string, object>
                    {
                        ["SourcePort"] = sourceEndpoint?.Port ?? 0,
                        ["Protocol"] = _udpClient != null ? "UDP" : "TCP"
                    }
                };

                // Parse RFC3164 or RFC5424 format
                ParseSyslogFormat(message, siemEvent);

                return siemEvent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to parse Syslog message: {message}");
                return null;
            }
        }

        private void ParseSyslogFormat(string message, SiemEvent siemEvent)
        {
            // RFC3164 format: <priority>timestamp hostname tag: message
            // RFC5424 format: <priority>version timestamp hostname app-name procid msgid structured-data msg

            var priorityMatch = Regex.Match(message, @"^<(\d+)>");
            if (priorityMatch.Success)
            {
                var priority = int.Parse(priorityMatch.Groups[1].Value);
                var facility = priority / 8;
                var severity = priority % 8;

                siemEvent.Severity = MapSyslogSeverityToSiem(severity);
                siemEvent.CustomFields["Facility"] = facility;
                siemEvent.CustomFields["SyslogSeverity"] = severity;

                // Remove priority from message
                message = message.Substring(priorityMatch.Length);
            }

            // Try to extract timestamp
            var timestampPatterns = new[]
            {
                @"^(\w{3}\s+\d{1,2}\s\d{2}:\d{2}:\d{2})",  // RFC3164: Jan 1 12:00:00
                @"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)"  // RFC5424: ISO timestamp
            };

            foreach (var pattern in timestampPatterns)
            {
                var match = Regex.Match(message, pattern);
                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var timestamp))
                {
                    siemEvent.Timestamp = timestamp;
                    message = message.Substring(match.Length).TrimStart();
                    break;
                }
            }

            // Try to extract hostname
            var hostnameMatch = Regex.Match(message, @"^(\S+)\s");
            if (hostnameMatch.Success)
            {
                siemEvent.SourceSystem = hostnameMatch.Groups[1].Value;
                message = message.Substring(hostnameMatch.Length);
            }

            // Try to extract tag/application
            var tagMatch = Regex.Match(message, @"^(\S+?):");
            if (tagMatch.Success)
            {
                siemEvent.CustomFields["Tag"] = tagMatch.Groups[1].Value;
                message = message.Substring(tagMatch.Length).TrimStart();
            }

            // Remaining message is the actual log content
            siemEvent.Description = message.Length > 500 ? message.Substring(0, 500) + "..." : message;
        }

        private string MapSyslogSeverityToSiem(int syslogSeverity)
        {
            return syslogSeverity switch
            {
                0 => "Critical",  // Emergency
                1 => "Critical",  // Alert
                2 => "Critical",  // Critical
                3 => "High",      // Error
                4 => "Medium",    // Warning
                5 => "Low",       // Notice
                6 => "Low",       // Informational
                7 => "Low",       // Debug
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
            _cancellationTokenSource?.Cancel();

            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            _tcpListener?.Stop();
            _tcpListener = null;

            _isRunning = false;
        }
    }
}
