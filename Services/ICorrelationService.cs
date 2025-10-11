using SiemCore.Models;

namespace SiemCore.Services
{
    /// <summary>
    /// Interface for correlation and threat detection service
    /// </summary>
    public interface ICorrelationService
    {
        Task AnalyzeEventAsync(SiemEvent siemEvent);
        Task<IEnumerable<CorrelationRule>> GetActiveRulesAsync();
        Task<CorrelationRule> CreateRuleAsync(CorrelationRule rule);
        Task<bool> UpdateRuleAsync(CorrelationRule rule);
        Task<bool> DeleteRuleAsync(Guid ruleId);
        Task<bool> EnableRuleAsync(Guid ruleId, bool enabled);
        Task RunCorrelationAnalysisAsync();
    }
}
