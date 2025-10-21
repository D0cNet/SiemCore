namespace SiemCore.Services
{
    /// <summary>
    /// Interface for threat intelligence service
    /// </summary>
    public interface IThreatIntelligenceService
    {
        Task<ThreatInfo?> CheckThreatAsync(string indicator);
        Task<bool> UpdateThreatFeedsAsync();
        Task<IEnumerable<ThreatIndicator>> GetThreatIndicatorsAsync();
    }

    public class ThreatInfo
    {
        public bool IsMalicious { get; set; }
        public string ThreatType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }
        public int Confidence { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class ThreatIndicator
    {
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public ThreatInfo ThreatInfo { get; set; } = new ThreatInfo();
    }
}
