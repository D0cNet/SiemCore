using SiemCore.Models;
using System.Collections.Concurrent;

namespace SiemCore.Services
{
    /// <summary>
    /// Service for managing SIEM alerts
    /// </summary>
    public class AlertService : IAlertService
    {
        private readonly ILogger<AlertService> _logger;
        private readonly ConcurrentDictionary<Guid, Alert> _alertStore;
        private readonly INotificationService _notificationService;

        public AlertService(
            ILogger<AlertService> logger,
            INotificationService notificationService)
        {
            _logger = logger;
            _notificationService = notificationService;
            _alertStore = new ConcurrentDictionary<Guid, Alert>();
        }

        public async Task<Alert> CreateAlertAsync(Alert alert)
        {
            try
            {
                alert.CreatedAt = DateTime.UtcNow;
                alert.Status = AlertStatus.Open;
                
                _alertStore.TryAdd(alert.Id, alert);
                
                // Send notification for high and critical alerts
                if (alert.Severity >= AlertSeverity.High)
                {
                    await _notificationService.SendAlertNotificationAsync(alert);
                }
                
                _logger.LogInformation($"Created alert {alert.Id}: {alert.Title}");
                
                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating alert: {alert.Title}");
                throw;
            }
        }

        public async Task<IEnumerable<Alert>> GetAlertsAsync(
            AlertStatus? status, 
            AlertSeverity? severity, 
            int page, 
            int pageSize)
        {
            await Task.CompletedTask; // Placeholder for async operation
            
            var query = _alertStore.Values.AsQueryable();
            
            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);
                
            if (severity.HasValue)
                query = query.Where(a => a.Severity == severity.Value);
            
            return query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public async Task<Alert?> GetAlertByIdAsync(Guid alertId)
        {
            await Task.CompletedTask; // Placeholder for async operation
            
            _alertStore.TryGetValue(alertId, out var alert);
            return alert;
        }

        public async Task<bool> UpdateAlertStatusAsync(Guid alertId, AlertStatus status, string resolution)
        {
            try
            {
                if (_alertStore.TryGetValue(alertId, out var alert))
                {
                    alert.Status = status;
                    alert.Resolution = resolution;
                    alert.UpdatedAt = DateTime.UtcNow;
                    
                    if (status == AlertStatus.Resolved || status == AlertStatus.Closed)
                    {
                        alert.ResolvedAt = DateTime.UtcNow;
                    }
                    
                    _logger.LogInformation($"Updated alert {alertId} status to {status}");
                    
                    await Task.CompletedTask;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating alert {alertId}");
                return false;
            }
        }

        public async Task<bool> AssignAlertAsync(Guid alertId, string assignedTo)
        {
            try
            {
                if (_alertStore.TryGetValue(alertId, out var alert))
                {
                    alert.AssignedTo = assignedTo;
                    alert.UpdatedAt = DateTime.UtcNow;
                    
                    if (alert.Status == AlertStatus.Open)
                    {
                        alert.Status = AlertStatus.InProgress;
                    }
                    
                    _logger.LogInformation($"Assigned alert {alertId} to {assignedTo}");
                    
                    await Task.CompletedTask;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning alert {alertId}");
                return false;
            }
        }

        public async Task<IEnumerable<Alert>> GetAlertsByRuleAsync(Guid ruleId)
        {
            await Task.CompletedTask; // Placeholder for async operation
            
            return _alertStore.Values
                .Where(a => a.RuleId == ruleId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
        }

        public async Task<bool> MarkAsFalsePositiveAsync(Guid alertId, string reason)
        {
            try
            {
                if (_alertStore.TryGetValue(alertId, out var alert))
                {
                    alert.Status = AlertStatus.FalsePositive;
                    alert.Resolution = reason;
                    alert.UpdatedAt = DateTime.UtcNow;
                    alert.ResolvedAt = DateTime.UtcNow;
                    
                    // Update the rule's false positive count
                    await UpdateRuleFalsePositiveCountAsync(alert.RuleId);
                    
                    _logger.LogInformation($"Marked alert {alertId} as false positive: {reason}");
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking alert {alertId} as false positive");
                return false;
            }
        }

        private async Task UpdateRuleFalsePositiveCountAsync(Guid ruleId)
        {
            // This would typically update the correlation rule's false positive count
            // For now, it's a placeholder
            await Task.CompletedTask;
            _logger.LogInformation($"Updated false positive count for rule {ruleId}");
        }
    }
}
