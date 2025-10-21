using SiemAgent.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SiemAgent.Services
{
    /// <summary>
    /// HTTP-based implementation of event forwarder service
    /// </summary>
    public class EventForwarderService : IEventForwarderService
    {
        private readonly ILogger<EventForwarderService> _logger;
        private readonly HttpClient _httpClient;
        private readonly AgentConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _isConnected = false;

        public bool IsConnected => _isConnected;

        public event EventHandler<bool>? ConnectionStatusChanged;

        public EventForwarderService(
            ILogger<EventForwarderService> logger,
            HttpClient httpClient,
            AgentConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            ConfigureHttpClient();
        }

        public async Task<bool> ForwardEventAsync(SiemEvent siemEvent)
        {
            try
            {
                // Set agent information
                siemEvent.AgentId = _configuration.AgentId;
                siemEvent.AgentVersion = _configuration.AgentVersion;

                var json = JsonSerializer.Serialize(siemEvent, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/siem/events", content);

                if (response.IsSuccessStatusCode)
                {
                    UpdateConnectionStatus(true);
                    _logger.LogDebug($"Successfully forwarded event {siemEvent.Id}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to forward event {siemEvent.Id}. Status: {response.StatusCode}");
                    UpdateConnectionStatus(false);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Network error forwarding event {siemEvent.Id}");
                UpdateConnectionStatus(false);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding event {siemEvent.Id}");
                return false;
            }
        }

        public async Task<bool> ForwardEventsAsync(IEnumerable<SiemEvent> siemEvents)
        {
            try
            {
                var eventList = siemEvents.ToList();
                if (!eventList.Any())
                    return true;

                // Set agent information for all events
                foreach (var siemEvent in eventList)
                {
                    siemEvent.AgentId = _configuration.AgentId;
                    siemEvent.AgentVersion = _configuration.AgentVersion;
                }

                var json = JsonSerializer.Serialize(eventList, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/siem/events/batch", content);

                if (response.IsSuccessStatusCode)
                {
                    UpdateConnectionStatus(true);
                    _logger.LogDebug($"Successfully forwarded {eventList.Count} events");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to forward {eventList.Count} events. Status: {response.StatusCode}");
                    UpdateConnectionStatus(false);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Network error forwarding {siemEvents.Count()} events");
                UpdateConnectionStatus(false);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding {siemEvents.Count()} events");
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                var isConnected = response.IsSuccessStatusCode;
                
                UpdateConnectionStatus(isConnected);
                
                if (isConnected)
                {
                    _logger.LogDebug("Connection test successful");
                }
                else
                {
                    _logger.LogWarning($"Connection test failed. Status: {response.StatusCode}");
                }

                return isConnected;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error during connection test");
                UpdateConnectionStatus(false);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection test");
                UpdateConnectionStatus(false);
                return false;
            }
        }

        public async Task<bool> SendHealthStatusAsync(AgentHealth healthStatus)
        {
            try
            {
                var json = JsonSerializer.Serialize(healthStatus, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/siem/agents/{_configuration.AgentId}/health", content);

                if (response.IsSuccessStatusCode)
                {
                    UpdateConnectionStatus(true);
                    _logger.LogDebug("Successfully sent health status");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to send health status. Status: {response.StatusCode}");
                    UpdateConnectionStatus(false);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error sending health status");
                UpdateConnectionStatus(false);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending health status");
                return false;
            }
        }

        public async Task<AgentConfiguration?> GetConfigurationAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/siem/agents/{_configuration.AgentId}/configuration");

                if (response.IsSuccessStatusCode)
                {
                    UpdateConnectionStatus(true);
                    var json = await response.Content.ReadAsStringAsync();
                    var configuration = JsonSerializer.Deserialize<AgentConfiguration>(json, _jsonOptions);
                    
                    _logger.LogDebug("Successfully retrieved configuration");
                    return configuration;
                }
                else
                {
                    _logger.LogWarning($"Failed to get configuration. Status: {response.StatusCode}");
                    UpdateConnectionStatus(false);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error getting configuration");
                UpdateConnectionStatus(false);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration");
                return null;
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_configuration.SiemCoreApiUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Add API key authentication
            if (!string.IsNullOrEmpty(_configuration.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
            }

            // Add user agent
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SiemAgent", _configuration.AgentVersion));

            // Add agent identification headers
            _httpClient.DefaultRequestHeaders.Add("X-Agent-Id", _configuration.AgentId);
            _httpClient.DefaultRequestHeaders.Add("X-Agent-Version", _configuration.AgentVersion);
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            if (_isConnected != isConnected)
            {
                _isConnected = isConnected;
                ConnectionStatusChanged?.Invoke(this, isConnected);
                
                _logger.LogInformation($"Connection status changed: {(isConnected ? "Connected" : "Disconnected")}");
            }
        }
    }
}
