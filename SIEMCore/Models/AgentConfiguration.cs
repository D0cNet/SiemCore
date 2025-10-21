using System.ComponentModel.DataAnnotations;

namespace SiemCore.Models
{
    /// <summary>
    /// Configuration model for SIEM agents
    /// </summary>
    public class AgentConfiguration
    {
        [Required]
        public string AgentId { get; set; } = string.Empty;

        [Required]
        public string AgentVersion { get; set; } = string.Empty;

        [Range(1, 10000)]
        public int EventBatchSize { get; set; } = 100;

        [Range(1, 3600)]
        public int EventFlushIntervalSeconds { get; set; } = 30;

        [Range(0, 10)]
        public int MaxRetryAttempts { get; set; } = 3;

        [Range(1, 300)]
        public int RetryDelaySeconds { get; set; } = 5;

        [Range(1, 1000000)]
        public int MaxCachedEvents { get; set; } = 10000;

        public bool EnableLocalAnalysis { get; set; } = true;

        public bool EnableEventFiltering { get; set; } = true;

        [Required]
        public string LogLevel { get; set; } = "Information";

        [Range(10, 3600)]
        public int HealthCheckIntervalSeconds { get; set; } = 60;

        [Range(60, 86400)]
        public int ConfigurationRefreshIntervalSeconds { get; set; } = 300;

        [Required]
        public string SiemCoreApiUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Dictionary<string, object> CustomSettings { get; set; } = new();

        /// <summary>
        /// Collector configurations for this agent
        /// </summary>
        public List<CollectorConfiguration> Collectors { get; set; } = new();
    }

    /// <summary>
    /// Configuration for individual event collectors
    /// </summary>
    public class CollectorConfiguration
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        [Range(1, 3600)]
        public int CollectionIntervalSeconds { get; set; } = 60;

        public Dictionary<string, object> Settings { get; set; } = new();

        public List<string> IncludePatterns { get; set; } = new();

        public List<string> ExcludePatterns { get; set; } = new();

        public string SeverityFilter { get; set; } = string.Empty;
    }

    /// <summary>
    /// Agent health status model
    /// </summary>
    public class AgentHealth
    {
        [Required]
        public string AgentId { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public DateTime LastConfigurationUpdate { get; set; }

        public int HealthCheckIntervalSeconds { get; set; } = 60;

        public long MemoryUsageMB { get; set; }

        public double CpuUsagePercent { get; set; }

        public int EventsProcessedLastHour { get; set; }

        public int CachedEventCount { get; set; }

        public int ErrorCount { get; set; }

        public string LastError { get; set; } = string.Empty;

        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
