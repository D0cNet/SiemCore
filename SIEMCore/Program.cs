using SiemCore.Services;
using SiemCore.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "SIEM Core API",
        Version = "v1",
        Description = "A comprehensive SIEM (Security Information and Event Management) system API with API key authentication"
    });

    // Add API Key authentication to Swagger
    //c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    //{
    //    Description = "API Key needed to access the endpoints. Format: Bearer {your-api-key}",
    //    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    //    Name = "Authorization",
    //    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
    //    Scheme = "Bearer"
    //});

    //c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    //{
    //    {
    //        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    //        {
    //            Reference = new Microsoft.OpenApi.Models.OpenApiReference
    //            {
    //                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
    //                Id = "ApiKey"
    //            }
    //        },
    //        Array.Empty<string>()
    //    }
    //});
});

// Register API Key Authentication
builder.Services.AddApiKeyAuthentication(builder.Configuration);

// Register SIEM services
builder.Services.AddSingleton<IEventProcessingService, EventProcessingService>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<ICorrelationService, CorrelationService>();
builder.Services.AddSingleton<IThreatIntelligenceService, ThreatIntelligenceService>();
builder.Services.AddSingleton<IMachineLearningService, MachineLearningService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<IAgentConfigurationService, AgentConfigurationService>();

// Add CORS for web frontend with configuration
builder.Services.AddCors(options =>
{
    var corsConfig = builder.Configuration.GetSection("Cors");
    var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" };
    var allowedMethods = corsConfig.GetSection("AllowedMethods").Get<string[]>() ?? new[] { "GET", "POST", "PUT", "DELETE" };
    var allowedHeaders = corsConfig.GetSection("AllowedHeaders").Get<string[]>() ?? new[] { "*" };

    options.AddPolicy("SiemCorsPolicy", policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.WithMethods(allowedMethods)
              .WithHeaders(allowedHeaders);
    });
});

// Add HTTP client for external services
builder.Services.AddHttpClient();

// Add memory cache for performance
builder.Services.AddMemoryCache();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("siem_core", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SIEM Core is running"));

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add request/response logging in development
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddFilter("Microsoft.AspNetCore.HttpLogging", LogLevel.Information);
    builder.Services.AddHttpLogging(options =>
    {
        options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders |
                               Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponsePropertiesAndHeaders;
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SIEM Core API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
        c.DocumentTitle = "SIEM Core API Documentation";
        c.DisplayRequestDuration();
    });

    // Add HTTP logging in development
    app.UseHttpLogging();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

    if (context.Request.IsHttps)
    {
        context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseHttpsRedirection();

app.UseCors("SiemCorsPolicy");

// Add API Key Authentication Middleware
app.UseApiKeyAuthentication();

app.UseAuthorization();

app.MapControllers();

// Add health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

//// Add a detailed system info endpoint (requires authentication)
//app.MapGet("/api/system/info", (HttpContext context) =>
//{
//    var agentId = context.User.FindFirst("AgentId")?.Value;
//    var agentVersion = context.User.FindFirst("AgentVersion")?.Value;

//    return new
//    {
//        Status = "Healthy",
//        Timestamp = DateTime.UtcNow,
//        Version = "1.0.0",
//        Environment = app.Environment.EnvironmentName,
//        RequestingAgent = new
//        {
//            Id = agentId ?? "Unknown",
//            Version = agentVersion ?? "Unknown"
//        },
//        SystemInfo = new
//        {
//            MachineName = Environment.MachineName,
//            ProcessorCount = Environment.ProcessorCount,
//            OSVersion = Environment.OSVersion.ToString(),
//            WorkingSet = Environment.WorkingSet,
//            TickCount = Environment.TickCount64
//        }
//    };
//}).RequireAuthorization();

//// Add agent management endpoints
//app.MapPost("/api/siem/agents/{agentId}/register", async (string agentId, HttpContext context) =>
//{
//    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
//    var agentVersion = context.Request.Headers["X-Agent-Version"].FirstOrDefault() ?? "Unknown";
//    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

//    logger.LogInformation("Agent registration: {AgentId} v{AgentVersion} from {RemoteIp}",
//        agentId, agentVersion, remoteIp);

//    return Results.Ok(new
//    {
//        Message = "Agent registered successfully",
//        AgentId = agentId,
//        RegisteredAt = DateTime.UtcNow,
//        NextHeartbeat = DateTime.UtcNow.AddMinutes(1)
//    });
//}).RequireAuthorization();

//app.MapGet("/api/siem/agents/{agentId}/configuration", (string agentId) =>
//{
//    // Return agent-specific configuration
//    return Results.Ok(new
//    {
//        AgentId = agentId,
//        Configuration = new
//        {
//            EventBatchSize = 100,
//            EventFlushIntervalSeconds = 30,
//            MaxRetryAttempts = 3,
//            RetryDelaySeconds = 5,
//            EnableLocalAnalysis = true,
//            LogLevel = "Information",
//            HealthCheckIntervalSeconds = 60
//        },
//        UpdatedAt = DateTime.UtcNow
//    });
//}).RequireAuthorization();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("SIEM Core API starting up...");
logger.LogInformation($"Environment: {app.Environment.EnvironmentName}");
logger.LogInformation($"Authentication: API Key authentication enabled");

// Log configured API keys (without revealing the actual keys)
var authConfig = app.Configuration.GetSection("Authentication:ApiKeys");
var apiKeyCount = authConfig.GetChildren().Count(k => k.GetValue<bool>("Enabled"));
logger.LogInformation($"Loaded {apiKeyCount} active API keys for agent authentication");

app.Run();
