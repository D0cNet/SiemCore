using SiemCore.Models;
using System.Collections.Concurrent;

namespace SiemCore.Services
{
    /// <summary>
    /// Service for correlation analysis and threat detection
    /// </summary>
    public class CorrelationService : ICorrelationService
    {
        private readonly ILogger<CorrelationService> _logger;
        private readonly ConcurrentDictionary<Guid, CorrelationRule> _ruleStore;
        private readonly IAlertService _alertService;
        private readonly IMachineLearningService _mlService;

        public CorrelationService(
            ILogger<CorrelationService> logger,
            IAlertService alertService,
            IMachineLearningService mlService)
        {
            _logger = logger;
            _alertService = alertService;
            _mlService = mlService;
            _ruleStore = new ConcurrentDictionary<Guid, CorrelationRule>();
            
            // Initialize with default rules
            InitializeDefaultRules();
        }

        public async Task AnalyzeEventAsync(SiemEvent siemEvent)
        {
            try
            {
                var activeRules = await GetActiveRulesAsync();
                
                foreach (var rule in activeRules)
                {
                    if (await EvaluateRuleAsync(rule, siemEvent))
                    {
                        await TriggerAlertAsync(rule, siemEvent);
                    }
                }
                
                // Run ML-based anomaly detection
                await _mlService.AnalyzeEventForAnomaliesAsync(siemEvent);
                
                siemEvent.IsCorrelated = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing event {siemEvent.Id}");
            }
        }

        public async Task<IEnumerable<CorrelationRule>> GetActiveRulesAsync()
        {
            await Task.CompletedTask; // Placeholder for async operation
            
            return _ruleStore.Values.Where(r => r.IsEnabled).ToList();
        }

        public async Task<CorrelationRule> CreateRuleAsync(CorrelationRule rule)
        {
            try
            {
                rule.CreatedAt = DateTime.UtcNow;
                _ruleStore.TryAdd(rule.Id, rule);
                
                _logger.LogInformation($"Created correlation rule: {rule.Name}");
                
                await Task.CompletedTask;
                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating rule: {rule.Name}");
                throw;
            }
        }

        public async Task<CorrelationRule> GetRuleByIdAsync(Guid ruleId)
        {
            try
            {
                if (_ruleStore.TryGetValue(ruleId, out var rule))
                {
                    await Task.CompletedTask;
                    return rule;
                }
                
                return null!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving rule {ruleId}");
                throw;
            }
        }
        public async Task<bool> UpdateRuleAsync(CorrelationRule rule)
        {
            try
            {
                if (_ruleStore.TryGetValue(rule.Id, out var existingRule))
                {
                    rule.UpdatedAt = DateTime.UtcNow;
                    _ruleStore.TryUpdate(rule.Id, rule, existingRule);
                    
                    _logger.LogInformation($"Updated correlation rule: {rule.Name}");
                    
                    await Task.CompletedTask;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rule {rule.Id}");
                return false;
            }
        }

       
        public async Task<bool> DeleteRuleAsync(Guid ruleId)
        {
            try
            {
                if (_ruleStore.TryRemove(ruleId, out var rule))
                {
                    _logger.LogInformation($"Deleted correlation rule: {rule.Name}");
                    await Task.CompletedTask;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting rule {ruleId}");
                return false;
            }
        }

        public async Task<bool> EnableRuleAsync(Guid ruleId, bool enabled)
        {
            try
            {
                if (_ruleStore.TryGetValue(ruleId, out var rule))
                {
                    rule.IsEnabled = enabled;
                    rule.UpdatedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation($"{(enabled ? "Enabled" : "Disabled")} correlation rule: {rule.Name}");
                    
                    await Task.CompletedTask;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error enabling/disabling rule {ruleId}");
                return false;
            }
        }

        public async Task RunCorrelationAnalysisAsync()
        {
            try
            {
                _logger.LogInformation("Starting correlation analysis");
                
                // This would typically run complex correlation queries
                // across the event store to identify patterns
                
                await Task.CompletedTask;
                
                _logger.LogInformation("Completed correlation analysis");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running correlation analysis");
            }
        }

        private async Task<bool> EvaluateRuleAsync(CorrelationRule rule, SiemEvent siemEvent)
        {
            try
            {
                // Simple rule evaluation based on event properties
                // In a real implementation, this would use a more sophisticated query engine
                
                if (rule.IsMLBased)
                {
                    return await _mlService.EvaluateMLRuleAsync(rule, siemEvent);
                }
                
                // Basic string matching for demonstration
                var matches = rule.Query.ToLower() switch
                {
                    var q when q.Contains("failed login") => 
                        siemEvent.EventType.Contains("login", StringComparison.OrdinalIgnoreCase) &&
                        siemEvent.Description.Contains("failed", StringComparison.OrdinalIgnoreCase),
                    
                    var q when q.Contains("brute force") =>
                        siemEvent.EventType.Contains("authentication", StringComparison.OrdinalIgnoreCase) &&
                        siemEvent.Severity == "High",
                    
                    var q when q.Contains("malware") =>
                        siemEvent.EventType.Contains("malware", StringComparison.OrdinalIgnoreCase) ||
                        siemEvent.Description.Contains("virus", StringComparison.OrdinalIgnoreCase),
                    
                    _ => false
                };
                
                return matches;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error evaluating rule {rule.Name} for event {siemEvent.Id}");
                return false;
            }
        }

        private async Task TriggerAlertAsync(CorrelationRule rule, SiemEvent siemEvent)
        {
            try
            {
                var alert = new Alert
                {
                    Title = $"Rule Triggered: {rule.Name}",
                    Description = $"Correlation rule '{rule.Name}' was triggered by event from {siemEvent.SourceSystem}",
                    Severity = rule.Severity,
                    RuleName = rule.Name,
                    RuleId = rule.Id,
                    RelatedEventIds = new List<Guid> { siemEvent.Id }
                };
                
                await _alertService.CreateAlertAsync(alert);
                
                // Update rule statistics
                rule.LastTriggered = DateTime.UtcNow;
                rule.TriggerCount++;
                
                _logger.LogInformation($"Alert created for rule: {rule.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating alert for rule {rule.Name}");
            }
        }

        private void InitializeDefaultRules()
        {
            var defaultRules = new[]
            {
                new CorrelationRule
                {
                    Name = "Multiple Failed Logins",
                    Description = "Detects multiple failed login attempts from the same source",
                    Query = "failed login",
                    Severity = AlertSeverity.Medium,
                    Category = "Authentication",
                    TimeWindow = 300,
                    Threshold = 5,
                    Tags = new List<string> { "brute-force", "authentication" }
                },
                new CorrelationRule
                {
                    Name = "Malware Detection",
                    Description = "Detects malware-related events",
                    Query = "malware",
                    Severity = AlertSeverity.High,
                    Category = "Malware",
                    TimeWindow = 60,
                    Threshold = 1,
                    Tags = new List<string> { "malware", "threat" }
                },
                new CorrelationRule
                {
                    Name = "Privilege Escalation",
                    Description = "Detects potential privilege escalation attempts",
                    Query = "privilege escalation",
                    Severity = AlertSeverity.High,
                    Category = "Privilege Escalation",
                    TimeWindow = 600,
                    Threshold = 1,
                    Tags = new List<string> { "privilege-escalation", "insider-threat" }
                }
            };

            foreach (var rule in defaultRules)
            {
                _ruleStore.TryAdd(rule.Id, rule);
            }
        }
    }
}
