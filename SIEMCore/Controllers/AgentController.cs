using Microsoft.AspNetCore.Mvc;
using SiemCore.Models;
using SiemCore.Services;

namespace SiemCore.Controllers
{
    [ApiController]
    [Route("api/siem/agents")]
    public class AgentController : ControllerBase
    {
        private readonly IAgentConfigurationService _agentConfigurationService;
        private readonly ILogger<AgentController> _logger;

        public AgentController(
            IAgentConfigurationService agentConfigurationService,
            ILogger<AgentController> logger)
        {
            _agentConfigurationService = agentConfigurationService;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new SIEM agent
        /// </summary>
        /// <param name="agentId">The unique identifier for the agent</param>
        /// <param name="registrationInfo">Agent registration information</param>
        /// <returns>Initial configuration for the agent</returns>
        [HttpPost("{agentId}/register")]
        public async Task<IActionResult> RegisterAgent(string agentId, [FromBody] AgentRegistrationInfo registrationInfo)
        {
            try
            {
                if (string.IsNullOrEmpty(agentId))
                {
                    return BadRequest(new { Error = "Agent ID is required" });
                }

                // Get agent info from headers if not in body
                if (string.IsNullOrEmpty(registrationInfo.AgentVersion))
                {
                    registrationInfo.AgentVersion = Request.Headers["X-Agent-Version"].FirstOrDefault() ?? "Unknown";
                }

                if (string.IsNullOrEmpty(registrationInfo.IpAddress))
                {
                    registrationInfo.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                }

                var configuration = await _agentConfigurationService.RegisterAgentAsync(agentId, registrationInfo);

                _logger.LogInformation("Agent {AgentId} registered successfully from {IpAddress}", 
                    agentId, registrationInfo.IpAddress);

                return Ok(new
                {
                    Message = "Agent registered successfully",
                    AgentId = agentId,
                    RegisteredAt = DateTime.UtcNow,
                    Configuration = configuration,
                    NextHeartbeat = DateTime.UtcNow.AddMinutes(1)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error during agent registration" });
            }
        }

        /// <summary>
        /// Gets the current configuration for a specific agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <returns>The agent's current configuration</returns>
        [HttpGet("{agentId}/configuration")]
        public async Task<IActionResult> GetAgentConfiguration(string agentId)
        {
            try
            {
                var configuration = await _agentConfigurationService.GetAgentConfigurationAsync(agentId);
                
                if (configuration == null)
                {
                    return NotFound(new { Error = $"Configuration not found for agent {agentId}" });
                }

                return Ok(new
                {
                    AgentId = agentId,
                    Configuration = configuration,
                    RetrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration for agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Updates the configuration for a specific agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <param name="configuration">The new configuration to apply</param>
        /// <returns>Success status and updated configuration</returns>
        [HttpPut("{agentId}/configuration")]
        public async Task<IActionResult> UpdateAgentConfiguration(string agentId, [FromBody] AgentConfiguration configuration)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate the configuration
                var validationResult = await _agentConfigurationService.ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new 
                    { 
                        Error = "Invalid configuration", 
                        Errors = validationResult.Errors,
                        Warnings = validationResult.Warnings
                    });
                }

                var success = await _agentConfigurationService.UpdateAgentConfigurationAsync(agentId, configuration);
                
                if (!success)
                {
                    return BadRequest(new { Error = "Failed to update agent configuration" });
                }

                _logger.LogInformation("Configuration updated for agent {AgentId}", agentId);

                return Ok(new
                {
                    Message = "Configuration updated successfully",
                    AgentId = agentId,
                    UpdatedAt = DateTime.UtcNow,
                    Configuration = configuration,
                    Warnings = validationResult.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration for agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Pushes updated configuration to a specific agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <param name="configuration">The configuration to push to the agent</param>
        /// <returns>Push operation result</returns>
        [HttpPost("{agentId}/configuration/push")]
        public async Task<IActionResult> PushConfigurationToAgent(string agentId, [FromBody] AgentConfiguration configuration)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // First update the stored configuration
                var updateSuccess = await _agentConfigurationService.UpdateAgentConfigurationAsync(agentId, configuration);
                if (!updateSuccess)
                {
                    return BadRequest(new { Error = "Failed to update stored configuration" });
                }

                // Then push to the agent
                var pushSuccess = await _agentConfigurationService.PushConfigurationToAgentAsync(agentId, configuration);

                return Ok(new
                {
                    Message = "Configuration push completed",
                    AgentId = agentId,
                    ConfigurationUpdated = updateSuccess,
                    PushSuccessful = pushSuccess,
                    PushedAt = DateTime.UtcNow,
                    Note = pushSuccess ? "Configuration successfully pushed to agent" : 
                           "Configuration updated locally but push to agent failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing configuration to agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Receives health status from an agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <param name="healthStatus">The agent's health status</param>
        /// <returns>Acknowledgment and any configuration updates</returns>
        [HttpPost("{agentId}/health")]
        public async Task<IActionResult> ReceiveHealthStatus(string agentId, [FromBody] AgentHealth healthStatus)
        {
            try
            {
                _logger.LogDebug("Received health status from agent {AgentId}: {Status}", 
                    agentId, healthStatus.Status);

                // Check if agent needs configuration update
                var currentConfig = await _agentConfigurationService.GetAgentConfigurationAsync(agentId);
                var configurationUpdateAvailable = currentConfig != null && 
                    currentConfig.UpdatedAt > healthStatus.LastConfigurationUpdate;

                return Ok(new
                {
                    Message = "Health status received",
                    AgentId = agentId,
                    ReceivedAt = DateTime.UtcNow,
                    NextHeartbeat = DateTime.UtcNow.AddSeconds(healthStatus.HealthCheckIntervalSeconds),
                    ConfigurationUpdateAvailable = configurationUpdateAvailable,
                    ConfigurationUrl = configurationUpdateAvailable ? 
                        $"/api/siem/agents/{agentId}/configuration" : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing health status from agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets all registered agents and their configurations
        /// </summary>
        /// <returns>List of all agent configurations</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllAgents()
        {
            try
            {
                var configurations = await _agentConfigurationService.GetAllAgentConfigurationsAsync();
                
                return Ok(new
                {
                    TotalAgents = configurations.Count(),
                    Agents = configurations.Select(c => new
                    {
                        c.AgentId,
                        c.AgentVersion,
                        c.UpdatedAt,
                        c.LogLevel,
                        c.EnableLocalAnalysis,
                        ConfigurationSize = System.Text.Json.JsonSerializer.Serialize(c).Length
                    }),
                    RetrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all agent configurations");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Removes an agent and its configuration
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <returns>Removal operation result</returns>
        [HttpDelete("{agentId}")]
        public async Task<IActionResult> RemoveAgent(string agentId)
        {
            try
            {
                var success = await _agentConfigurationService.RemoveAgentAsync(agentId);
                
                if (!success)
                {
                    return NotFound(new { Error = $"Agent {agentId} not found" });
                }

                _logger.LogInformation("Agent {AgentId} removed successfully", agentId);

                return Ok(new
                {
                    Message = "Agent removed successfully",
                    AgentId = agentId,
                    RemovedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets the configuration update history for an agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <returns>Configuration update history</returns>
        [HttpGet("{agentId}/configuration/history")]
        public async Task<IActionResult> GetConfigurationHistory(string agentId)
        {
            try
            {
                var history = await _agentConfigurationService.GetConfigurationHistoryAsync(agentId);
                
                return Ok(new
                {
                    AgentId = agentId,
                    TotalUpdates = history.Count(),
                    History = history.Select(h => new
                    {
                        h.Id,
                        h.UpdatedAt,
                        h.UpdatedBy,
                        h.ChangeDescription,
                        h.PushSuccessful,
                        h.ErrorMessage,
                        HasPreviousConfig = h.PreviousConfiguration != null,
                        HasNewConfig = h.NewConfiguration != null
                    }),
                    RetrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration history for agent {AgentId}", agentId);
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Pushes configuration updates to all registered agents
        /// </summary>
        /// <param name="configuration">The configuration to push to all agents</param>
        /// <returns>Bulk push operation results</returns>
        [HttpPost("configuration/push-all")]
        public async Task<IActionResult> PushConfigurationToAllAgents([FromBody] AgentConfiguration configuration)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var results = await _agentConfigurationService.PushConfigurationToAllAgentsAsync(configuration);
                
                var successCount = results.Values.Count(r => r);
                var failureCount = results.Count - successCount;

                return Ok(new
                {
                    Message = "Bulk configuration push completed",
                    TotalAgents = results.Count,
                    SuccessfulPushes = successCount,
                    FailedPushes = failureCount,
                    Results = results,
                    PushedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing configuration to all agents");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets the default configuration template
        /// </summary>
        /// <returns>The default agent configuration</returns>
        [HttpGet("configuration/default")]
        public async Task<IActionResult> GetDefaultConfiguration()
        {
            try
            {
                var defaultConfig = await _agentConfigurationService.GetDefaultConfigurationAsync();
                
                return Ok(new
                {
                    Message = "Default configuration retrieved",
                    Configuration = defaultConfig,
                    RetrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default configuration");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Updates the default configuration template
        /// </summary>
        /// <param name="configuration">The new default configuration</param>
        /// <returns>Update operation result</returns>
        [HttpPut("configuration/default")]
        public async Task<IActionResult> UpdateDefaultConfiguration([FromBody] AgentConfiguration configuration)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _agentConfigurationService.UpdateDefaultConfigurationAsync(configuration);
                
                if (!success)
                {
                    return BadRequest(new { Error = "Failed to update default configuration" });
                }

                return Ok(new
                {
                    Message = "Default configuration updated successfully",
                    Configuration = configuration,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating default configuration");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }
    }
}
