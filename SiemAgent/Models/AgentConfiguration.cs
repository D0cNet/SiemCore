namespace SiemAgent.Models
{
    /// <summary>
    /// Configuration settings for the SIEM Agent
    /// </summary>
    public class AgentConfiguration
    {
        public string AgentId { get; set; } = Environment.MachineName;

        public string AgentVersion { get; set; } = "1.0.0";

        public string SiemCoreApiUrl { get; set; } = "https://localhost:5001";

        public string ApiKey { get; set; } = string.Empty;

        public int EventBatchSize { get; set; } = 100;

        public int EventFlushIntervalSeconds { get; set; } = 30;

        public int MaxRetryAttempts { get; set; } = 3;

        public int RetryDelaySeconds { get; set; } = 5;

        public int MaxCachedEvents { get; set; } = 10000;

        public bool EnableLocalAnalysis { get; set; } = true;

        public bool EnableEventFiltering { get; set; } = true;

        public List<CollectorConfiguration> Collectors { get; set; } = new List<CollectorConfiguration>();

        public Dictionary<string, string> CustomSettings { get; set; } = new Dictionary<string, string>();

        public string LogLevel { get; set; } = "Information";

        public int HealthCheckIntervalSeconds { get; set; } = 60;

        public int ConfigurationRefreshIntervalSeconds { get; set; } = 300;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Configuration for individual event collectors
    /// </summary>
    public class CollectorConfiguration
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public int CollectionIntervalSeconds { get; set; } = 60;

        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();

        public List<string> IncludePatterns { get; set; } = new List<string>();

        public List<string> ExcludePatterns { get; set; } = new List<string>();

        public string SeverityFilter { get; set; } = string.Empty;
    }
}
