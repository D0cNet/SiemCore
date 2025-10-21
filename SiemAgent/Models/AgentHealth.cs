namespace SiemAgent.Models
{
    /// <summary>
    /// Represents the health status of the SIEM Agent
    /// </summary>
    public class AgentHealth
    {
        public string AgentId { get; set; } = string.Empty;
        
        public string AgentVersion { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public AgentStatus Status { get; set; } = AgentStatus.Unknown;
        
        public string StatusMessage { get; set; } = string.Empty;
        
        public DateTime StartTime { get; set; }
        
        public TimeSpan Uptime => DateTime.UtcNow - StartTime;
        
        public long EventsCollected { get; set; } = 0;
        
        public long EventsForwarded { get; set; } = 0;
        
        public long EventsCached { get; set; } = 0;
        
        public long EventsFiltered { get; set; } = 0;
        
        public double CpuUsagePercent { get; set; } = 0.0;
        
        public long MemoryUsageBytes { get; set; } = 0;
        
        public long DiskUsageBytes { get; set; } = 0;
        
        public bool IsConnectedToSiemCore { get; set; } = false;
        
        public DateTime? LastSuccessfulConnection { get; set; }
        
        public DateTime? LastConfigurationUpdate { get; set; }
        
        public List<CollectorHealth> CollectorStatuses { get; set; } = new List<CollectorHealth>();
        
        public List<string> Errors { get; set; } = new List<string>();
        
        public List<string> Warnings { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Health status for individual collectors
    /// </summary>
    public class CollectorHealth
    {
        public string Name { get; set; } = string.Empty;
        
        public string Type { get; set; } = string.Empty;
        
        public AgentStatus Status { get; set; } = AgentStatus.Unknown;
        
        public string StatusMessage { get; set; } = string.Empty;
        
        public long EventsCollected { get; set; } = 0;
        
        public DateTime? LastCollection { get; set; }
        
        public string LastError { get; set; } = string.Empty;
        public DateTime LastEventTime { get; set; }
        public int ErrorCount { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
    }
    
    public enum AgentStatus
    {
        Unknown,
        Starting,
        Running,
        Warning,
        Error,
        Stopping,
        Stopped
    }
}
