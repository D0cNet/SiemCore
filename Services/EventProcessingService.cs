using SiemCore.Models;
using System.Collections.Concurrent;

namespace SiemCore.Services
{
    /// <summary>
    /// Service for processing and managing SIEM events
    /// </summary>
    public class EventProcessingService : IEventProcessingService
    {
        private readonly ILogger<EventProcessingService> _logger;
        private readonly ConcurrentDictionary<Guid, SiemEvent> _eventStore;
        private readonly IThreatIntelligenceService _threatIntelligenceService;

        public EventProcessingService(
            ILogger<EventProcessingService> logger,
            IThreatIntelligenceService threatIntelligenceService)
        {
            _logger = logger;
            _threatIntelligenceService = threatIntelligenceService;
            _eventStore = new ConcurrentDictionary<Guid, SiemEvent>();
        }

        public async Task<SiemEvent> ProcessEventAsync(SiemEvent siemEvent)
        {
            try
            {
                // Normalize the event
                await NormalizeEventAsync(siemEvent);
                
                // Enrich the event with additional context
                await EnrichEventAsync(siemEvent);
                
                // Store the event
                siemEvent.IsProcessed = true;
                siemEvent.UpdatedAt = DateTime.UtcNow;
                
                _eventStore.TryAdd(siemEvent.Id, siemEvent);
                
                _logger.LogInformation($"Processed event {siemEvent.Id} from {siemEvent.SourceSystem}");
                
                return siemEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing event {siemEvent.Id}");
                throw;
            }
        }

        public async Task<IEnumerable<SiemEvent>> SearchEventsAsync(
            DateTime? startTime, 
            DateTime? endTime, 
            string? sourceSystem, 
            string? eventType, 
            string? severity, 
            int page, 
            int pageSize)
        {
            await Task.CompletedTask; // Placeholder for async operation
            
            var query = _eventStore.Values.AsQueryable();
            
            if (startTime.HasValue)
                query = query.Where(e => e.Timestamp >= startTime.Value);
                
            if (endTime.HasValue)
                query = query.Where(e => e.Timestamp <= endTime.Value);
                
            if (!string.IsNullOrEmpty(sourceSystem))
                query = query.Where(e => e.SourceSystem.Contains(sourceSystem, StringComparison.OrdinalIgnoreCase));
                
            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType.Contains(eventType, StringComparison.OrdinalIgnoreCase));
                
            if (!string.IsNullOrEmpty(severity))
                query = query.Where(e => e.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));
            
            return query
                .OrderByDescending(e => e.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public async Task<object> GetDashboardStatsAsync()
        {
            await Task.CompletedTask; // Placeholder for async operation
            
            var totalEvents = _eventStore.Count;
            var last24Hours = DateTime.UtcNow.AddHours(-24);
            var recentEvents = _eventStore.Values.Count(e => e.Timestamp >= last24Hours);
            
            var severityStats = _eventStore.Values
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var sourceStats = _eventStore.Values
                .GroupBy(e => e.SourceSystem)
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());
            
            return new
            {
                TotalEvents = totalEvents,
                RecentEvents = recentEvents,
                SeverityDistribution = severityStats,
                TopSources = sourceStats,
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<bool> EnrichEventAsync(SiemEvent siemEvent)
        {
            try
            {
                // Enrich with geolocation data for IP addresses
                if (!string.IsNullOrEmpty(siemEvent.SourceIp))
                {
                    var geoInfo = await GetGeolocationAsync(siemEvent.SourceIp);
                    if (geoInfo != null)
                    {
                        siemEvent.CustomFields["SourceGeoLocation"] = geoInfo;
                    }
                }
                
                // Enrich with threat intelligence
                if (!string.IsNullOrEmpty(siemEvent.SourceIp))
                {
                    var threatInfo = await _threatIntelligenceService.CheckThreatAsync(siemEvent.SourceIp);
                    if (threatInfo != null)
                    {
                        siemEvent.CustomFields["ThreatIntelligence"] = threatInfo;
                        if (threatInfo.IsMalicious)
                        {
                            siemEvent.Severity = "High";
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to enrich event {siemEvent.Id}");
                return false;
            }
        }

        public async Task<bool> NormalizeEventAsync(SiemEvent siemEvent)
        {
            try
            {
                // Normalize timestamp to UTC
                if (siemEvent.Timestamp.Kind != DateTimeKind.Utc)
                {
                    siemEvent.Timestamp = siemEvent.Timestamp.ToUniversalTime();
                }
                
                // Normalize severity levels
                siemEvent.Severity = NormalizeSeverity(siemEvent.Severity);
                
                // Normalize IP addresses
                siemEvent.SourceIp = NormalizeIpAddress(siemEvent.SourceIp);
                siemEvent.DestinationIp = NormalizeIpAddress(siemEvent.DestinationIp);
                
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to normalize event {siemEvent.Id}");
                return false;
            }
        }

        private string NormalizeSeverity(string severity)
        {
            return severity?.ToLower() switch
            {
                "1" or "low" or "info" or "informational" => "Low",
                "2" or "medium" or "warn" or "warning" => "Medium",
                "3" or "high" or "error" => "High",
                "4" or "critical" or "fatal" => "Critical",
                _ => "Medium"
            };
        }

        private string NormalizeIpAddress(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return string.Empty;
                
            // Basic IP address validation and normalization
            if (System.Net.IPAddress.TryParse(ipAddress.Trim(), out var ip))
            {
                return ip.ToString();
            }
            
            return ipAddress;
        }

        private async Task<object?> GetGeolocationAsync(string ipAddress)
        {
            // Placeholder for geolocation service integration
            await Task.Delay(10);
            
            // Mock geolocation data
            return new
            {
                Country = "Unknown",
                City = "Unknown",
                Latitude = 0.0,
                Longitude = 0.0
            };
        }
    }
}
