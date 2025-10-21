using SiemCore.Models;

namespace SiemCore.Services
{
    /// <summary>
    /// Interface for machine learning and anomaly detection service
    /// </summary>
    public interface IMachineLearningService
    {
        Task AnalyzeEventForAnomaliesAsync(SiemEvent siemEvent);
        Task<bool> EvaluateMLRuleAsync(CorrelationRule rule, SiemEvent siemEvent);
        Task TrainAnomalyDetectionModelAsync();
        Task<double> CalculateAnomalyScoreAsync(SiemEvent siemEvent);
        Task<IEnumerable<UserBehaviorAnomaly>> DetectUserBehaviorAnomaliesAsync();
    }

    public class UserBehaviorAnomaly
    {
        public string Username { get; set; } = string.Empty;
        public string AnomalyType { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime DetectedAt { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
