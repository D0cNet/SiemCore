using System.ComponentModel.DataAnnotations;

namespace SiemCore.Models
{
    /// <summary>
    /// Represents a correlation rule for threat detection
    /// </summary>
    public class CorrelationRule
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public string Query { get; set; } = string.Empty;
        
        [Required]
        public AlertSeverity Severity { get; set; }
        
        public bool IsEnabled { get; set; } = true;
        
        public int TimeWindow { get; set; } = 300; // seconds
        
        public int Threshold { get; set; } = 1;
        
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;
        
        public List<string> Tags { get; set; } = new List<string>();
        
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        
        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public DateTime? LastTriggered { get; set; }
        
        public int TriggerCount { get; set; } = 0;
        
        public int FalsePositiveCount { get; set; } = 0;
        
        public double Accuracy { get; set; } = 0.0;
        
        public bool IsMLBased { get; set; } = false;
        
        public string MLModelPath { get; set; } = string.Empty;
        public int Priority { get; set; }
    }
}
