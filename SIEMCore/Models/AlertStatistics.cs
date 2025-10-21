namespace SiemCore.Models
{
    /// <summary>
    /// Represents statistics about alerts
    /// </summary>
    public class AlertStatistics
    {
        public long TotalAlerts { get; set; }
        public long OpenAlerts { get; set; }
        public long CriticalAlerts { get; set; }
        public long HighAlerts { get; set; }
        public long MediumAlerts { get; set; }
        public long LowAlerts { get; set; }
        public long AlertsToday { get; set; }
        public long AlertsThisHour { get; set; }
        public TimeSpan AverageResolutionTime { get; set; }
        public Dictionary<string, long> AlertsByStatus { get; set; } = new Dictionary<string, long>();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public int FalsePositives { get; set; }
        public int ResolvedAlerts { get; set; }
        public Dictionary<string, int> TopAlertTypes { get; set; }
        public Dictionary<string, int> AlertsBySeverity { get; set; }
    }
}
