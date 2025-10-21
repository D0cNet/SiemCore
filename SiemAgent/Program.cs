using Microsoft.AspNetCore.Builder;
using SiemAgent;
using SiemAgent.Collectors;
using SiemAgent.Models;
using SiemAgent.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure agent configuration
var agentConfig = new AgentConfiguration();
builder.Configuration.Bind("Agent", agentConfig);

// Set default values if not configured
if (string.IsNullOrEmpty(agentConfig.AgentId))
{
    agentConfig.AgentId = Environment.MachineName;
}

if (string.IsNullOrEmpty(agentConfig.SiemCoreApiUrl))
{
    agentConfig.SiemCoreApiUrl = builder.Configuration.GetValue<string>("SiemCore:ApiUrl") ?? "https://localhost:5001";
}

if (string.IsNullOrEmpty(agentConfig.ApiKey))
{
    agentConfig.ApiKey = builder.Configuration.GetValue<string>("SiemCore:ApiKey") ?? "";
}

// Add default collectors if none configured
if (!agentConfig.Collectors.Any())
{
    agentConfig.Collectors.AddRange(new[]
    {
        new CollectorConfiguration
        {
            Name = "File Log Collector",
            Type = "FileLog",
            Enabled = true,
            CollectionIntervalSeconds = 60,
            Settings = new Dictionary<string, object>
            {
                ["LogPaths"] = new List<string> { "/var/log/*.log", "/var/log/syslog" }
            }
        },
        new CollectorConfiguration
        {
            Name = "Windows Event Log Collector",
            Type = "WindowsEventLog",
            Enabled = OperatingSystem.IsWindows(),
            CollectionIntervalSeconds = 30,
            Settings = new Dictionary<string, object>
            {
                ["LogName"] = "Security",
                ["Query"] = "*"
            }
        },
        new CollectorConfiguration
        {
            Name = "Syslog Collector",
            Type = "Syslog",
            Enabled = false, // Disabled by default
            CollectionIntervalSeconds = 1,
            Settings = new Dictionary<string, object>
            {
                ["Port"] = 514,
                ["Protocol"] = "UDP"
            }
        }
    });
}

// Register configuration as singleton
builder.Services.AddSingleton(agentConfig);

// Register HTTP client for event forwarding
builder.Services.AddHttpClient<IEventForwarderService, EventForwarderService>();

// Register services
builder.Services.AddSingleton<IEventCacheService, EventCacheService>();
builder.Services.AddSingleton<IEventForwarderService, EventForwarderService>();
builder.Services.AddSingleton<IAgentHealthService, AgentHealthService>();
builder.Services.AddSingleton<IConfigurationUpdateService, ConfigurationUpdateService>();

// Add controllers and API support
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register collectors
builder.Services.AddSingleton<IEventCollector, FileLogCollector>();
builder.Services.AddSingleton<IEventCollector, WindowsEventLogCollector>();
builder.Services.AddSingleton<IEventCollector, SyslogCollector>();

// Register the main worker service
builder.Services.AddHostedService<Worker>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Set log level from configuration
var logLevelString = agentConfig.LogLevel;
if (Enum.TryParse<LogLevel>(logLevelString, true, out var logLevel))
{
    builder.Logging.SetMinimumLevel(logLevel);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();

// Add health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("SIEM Agent starting...");
logger.LogInformation($"Agent ID: {agentConfig.AgentId}");
logger.LogInformation($"Agent Version: {agentConfig.AgentVersion}");
logger.LogInformation($"SIEM Core URL: {agentConfig.SiemCoreApiUrl}");
logger.LogInformation($"Enabled Collectors: {agentConfig.Collectors.Count(c => c.Enabled)}");

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "SIEM Agent failed to start");
    throw;
}
