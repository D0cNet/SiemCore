using System.ComponentModel.DataAnnotations;

namespace SiemCore.Models
{
    /// <summary>
    /// Represents a security event in the SIEM system
    /// </summary>
    public class SiemEvent
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        [Required]
        [StringLength(100)]
        public string SourceSystem { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string EventType { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Severity { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(45)]
        public string SourceIp { get; set; } = string.Empty;
        
        [StringLength(45)]
        public string DestinationIp { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;
        
        public string RawLog { get; set; } = string.Empty;
        
        public Dictionary<string, object> CustomFields { get; set; } = new Dictionary<string, object>();
        
        public bool IsProcessed { get; set; } = false;
        
        public bool IsCorrelated { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        //Agent-specific fields
        public string AgentId { get; set; } = string.Empty;

        public string AgentVersion { get; set; } = string.Empty;

        public bool IsCached { get; set; } = false;

        public int RetryCount { get; set; } = 0;
        public string Processname { get; set; } = string.Empty;

    }
}
