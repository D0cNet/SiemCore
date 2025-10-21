using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace SiemCore.Middleware
{
    /// <summary>
    /// Middleware for API key authentication for SIEM agents
    /// </summary>
    public class ApiKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
        private readonly HashSet<string> _validApiKeys;
        private readonly HashSet<string> _exemptPaths;

        public ApiKeyAuthenticationMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ApiKeyAuthenticationMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;

            // Load valid API keys from configuration
            _validApiKeys = LoadValidApiKeys();

            // Define paths that don't require API key authentication
            _exemptPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "/health",
                "/swagger",
                "/swagger/v1/swagger.json",
                "/swagger/index.html"
            };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if the path is exempt from authentication
            if (IsExemptPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Extract API key from Authorization header
            string? apiKey = ExtractApiKey(context.Request);

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Missing API key in request from {RemoteIpAddress}", 
                    context.Connection.RemoteIpAddress);
                await WriteUnauthorizedResponse(context, "Missing API key");
                return;
            }

            // Validate API key
            if (!_validApiKeys.Contains(apiKey))
            {
                _logger.LogWarning("Invalid API key attempted from {RemoteIpAddress}: {ApiKey}", 
                    context.Connection.RemoteIpAddress, apiKey[..8] + "...");
                await WriteUnauthorizedResponse(context, "Invalid API key");
                return;
            }

            // Extract agent information from headers
            string? agentId = context.Request.Headers["X-Agent-Id"].FirstOrDefault();
            string? agentVersion = context.Request.Headers["X-Agent-Version"].FirstOrDefault();

            // Create claims for the authenticated agent
            var claims = new List<Claim>
            {
                new Claim("ApiKey", apiKey),
                new Claim("AuthenticationType", "ApiKey")
            };

            if (!string.IsNullOrEmpty(agentId))
            {
                claims.Add(new Claim("AgentId", agentId));
            }

            if (!string.IsNullOrEmpty(agentVersion))
            {
                claims.Add(new Claim("AgentVersion", agentVersion));
            }

            // Set the user identity
            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);

            _logger.LogDebug("Successfully authenticated agent {AgentId} with API key", agentId ?? "Unknown");

            await _next(context);
        }

        private HashSet<string> LoadValidApiKeys()
        {
            var apiKeys = new HashSet<string>();

            // Load from configuration section
            var apiKeySection = _configuration.GetSection("Authentication:ApiKeys");
            
            // Load individual API keys
            foreach (var keyConfig in apiKeySection.GetChildren())
            {
                var key = keyConfig.GetValue<string>("Key");
                var description = keyConfig.GetValue<string>("Description");
                var enabled = keyConfig.GetValue<bool>("Enabled");

                if (!string.IsNullOrEmpty(key) && enabled)
                {
                    apiKeys.Add(key);
                    _logger.LogInformation("Loaded API key for: {Description}", description ?? "Unknown");
                }
            }

            // Load from simple array format (backward compatibility)
            var simpleKeys = _configuration.GetSection("Authentication:ValidApiKeys").Get<string[]>();
            if (simpleKeys != null)
            {
                foreach (var key in simpleKeys.Where(k => !string.IsNullOrEmpty(k)))
                {
                    apiKeys.Add(key);
                }
            }

            _logger.LogInformation("Loaded {Count} valid API keys", apiKeys.Count);
            return apiKeys;
        }

        private bool IsExemptPath(PathString path)
        {
            return _exemptPaths.Any(exemptPath => 
                path.StartsWithSegments(exemptPath, StringComparison.OrdinalIgnoreCase));
        }

        private string? ExtractApiKey(HttpRequest request)
        {
            // Try Authorization header with Bearer scheme
            var authHeader = request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // Try custom X-API-Key header
            var apiKeyHeader = request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader;
            }

            // Try query parameter (less secure, mainly for testing)
            var queryApiKey = request.Query["apikey"].FirstOrDefault();
            if (!string.IsNullOrEmpty(queryApiKey))
            {
                return queryApiKey;
            }

            return null;
        }

        private async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var response = new
            {
                Error = "Unauthorized",
                Message = message,
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path.Value
            };

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }

    /// <summary>
    /// Extension methods for registering API key authentication middleware
    /// </summary>
    public static class ApiKeyAuthenticationExtensions
    {
        public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        }

        public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Register any additional services needed for API key authentication
            services.Configure<ApiKeyAuthenticationOptions>(configuration.GetSection("Authentication"));
            
            return services;
        }
    }

    /// <summary>
    /// Configuration options for API key authentication
    /// </summary>
    public class ApiKeyAuthenticationOptions
    {
        public List<ApiKeyConfig> ApiKeys { get; set; } = new();
        public string[] ValidApiKeys { get; set; } = Array.Empty<string>();
        public bool RequireHttps { get; set; } = true;
        public bool LogFailedAttempts { get; set; } = true;
    }

    /// <summary>
    /// Configuration for individual API keys
    /// </summary>
    public class ApiKeyConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public DateTime? ExpiresAt { get; set; }
        public string[] AllowedEndpoints { get; set; } = Array.Empty<string>();
        public string[] AllowedIpRanges { get; set; } = Array.Empty<string>();
    }
}
