using System.ComponentModel.DataAnnotations;

namespace SiemCore.Models
{
    /// <summary>
    /// Represents a data source configuration in the SIEM system
    /// </summary>
    public class DataSource
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public DataSourceType Type { get; set; }
        
        [Required]
        [StringLength(200)]
        public string ConnectionString { get; set; } = string.Empty;
        
        public bool IsEnabled { get; set; } = true;
        
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
        
        [StringLength(100)]
        public string Parser { get; set; } = string.Empty;
        
        public int CollectionInterval { get; set; } = 60; // seconds
        
        public DateTime? LastCollected { get; set; }
        
        public long EventsCollected { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public DataSourceStatus Status { get; set; } = DataSourceStatus.Active;
        
        public string LastError { get; set; } = string.Empty;
        
        public List<string> Tags { get; set; } = new List<string>();
        
        public bool UseSSL { get; set; } = false;
        
        public string AuthenticationMethod { get; set; } = string.Empty;
        
        public Dictionary<string, string> Credentials { get; set; } = new Dictionary<string, string>();
        public DateTime LastConnected { get; set; }
    }
    
    public enum DataSourceType
    {
        Syslog,
        WindowsEventLog,
        FileLog,
        Database,
        API,
        SNMP,
        NetFlow,
        Custom
    }
    
    public enum DataSourceStatus
    {
        Active,
        Inactive,
        Error,
        Maintenance
    }
}
