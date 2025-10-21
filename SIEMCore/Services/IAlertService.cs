using SiemCore.Models;

namespace SiemCore.Services
{
    /// <summary>
    /// Interface for alert management service
    /// </summary>
    public interface IAlertService
    {
        Task<Alert> CreateAlertAsync(Alert alert);
        Task<IEnumerable<Alert>> GetAlertsAsync(AlertStatus? status, AlertSeverity? severity, int page, int pageSize);
        Task<Alert?> GetAlertByIdAsync(Guid alertId);
        Task<bool> UpdateAlertStatusAsync(Guid alertId, AlertStatus status, string resolution);
        Task<bool> AssignAlertAsync(Guid alertId, string assignedTo);
        Task<IEnumerable<Alert>> GetAlertsByRuleAsync(Guid ruleId);
        Task<bool> MarkAsFalsePositiveAsync(Guid alertId, string reason);
    }
}
