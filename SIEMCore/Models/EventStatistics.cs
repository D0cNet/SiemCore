namespace SiemCore.Models
{
    /// <summary>
    /// Represents statistics about event processing
    /// </summary>
    public class EventStatistics
    {
        public long TotalEventsProcessed { get; set; }
        public long EventsProcessedToday { get; set; }
        public long EventsProcessedThisHour { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public Dictionary<string, long> EventsByType { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> EventsBySeverity { get; set; } = new Dictionary<string, long>();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public int AverageEventsPerHour { get; set; }
        public Dictionary<string, int> TopAgents { get; set; }
    }
}
