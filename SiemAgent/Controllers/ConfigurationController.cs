using Microsoft.AspNetCore.Mvc;
using SiemAgent.Models;
using SiemAgent.Services;
using System.Diagnostics;

namespace SiemAgent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationUpdateService _configurationUpdateService;
        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(
            IConfigurationUpdateService configurationUpdateService,
            ILogger<ConfigurationController> logger)
        {
            _configurationUpdateService = configurationUpdateService;
            _logger = logger;
        }

        /// <summary>
        /// Receives configuration updates from SiemCore
        /// </summary>
        /// <param name="configuration">The new configuration to apply</param>
        /// <returns>Update operation result</returns>
        [HttpPost("update")]
        public async Task<IActionResult> UpdateConfiguration([FromBody] AgentConfiguration configuration)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new 
                    { 
                        Error = "Invalid configuration data", 
                        Details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) 
                    });
                }

                _logger.LogInformation("Received configuration update request for agent {AgentId}", configuration.AgentId);

                // Validate the configuration
                var validationResult = await _configurationUpdateService.ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Configuration validation failed: {Errors}", 
                        string.Join(", ", validationResult.Errors));
                    
                    return BadRequest(new 
                    { 
                        Error = "Configuration validation failed", 
                        Errors = validationResult.Errors,
                        Warnings = validationResult.Warnings
                    });
                }

                // Apply the configuration
                var success = await _configurationUpdateService.ApplyConfigurationAsync(configuration);
                
                if (success)
                {
                    _logger.LogInformation("Configuration updated successfully for agent {AgentId}", configuration.AgentId);
                    
                    return Ok(new
                    {
                        Message = "Configuration updated successfully",
                        AgentId = configuration.AgentId,
                        UpdatedAt = DateTime.UtcNow,
                        RestartRequired = validationResult.RestartRequired,
                        Warnings = validationResult.Warnings
                    });
                }
                else
                {
                    _logger.LogError("Failed to apply configuration for agent {AgentId}", configuration.AgentId);
                    return StatusCode(500, new { Error = "Failed to apply configuration" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing configuration update");
                return StatusCode(500, new { Error = "Internal server error during configuration update" });
            }
        }

        /// <summary>
        /// Gets the current agent configuration
        /// </summary>
        /// <returns>Current configuration</returns>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentConfiguration()
        {
            try
            {
                var configuration = await _configurationUpdateService.GetCurrentConfigurationAsync();
                
                return Ok(new
                {
                    Message = "Current configuration retrieved",
                    Configuration = configuration,
                    RetrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current configuration");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }

        /// <summary>
        /// Validates a configuration without applying it
        /// </summary>
        /// <param name="configuration">The configuration to validate</param>
        /// <returns>Validation result</returns>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateConfiguration([FromBody] AgentConfiguration configuration)
        {
            try
            {
                var validationResult = await _configurationUpdateService.ValidateConfigurationAsync(configuration);
                
                return Ok(new
                {
                    Message = "Configuration validation completed",
                    IsValid = validationResult.IsValid,
                    Errors = validationResult.Errors,
                    Warnings = validationResult.Warnings,
                    RestartRequired = validationResult.RestartRequired,
                    ValidatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration");
                return StatusCode(500, new { Error = "Internal server error during validation" });
            }
        }

        /// <summary>
        /// Backs up the current configuration
        /// </summary>
        /// <returns>Backup operation result</returns>
        [HttpPost("backup")]
        public async Task<IActionResult> BackupConfiguration()
        {
            try
            {
                var success = await _configurationUpdateService.BackupCurrentConfigurationAsync();
                
                if (success)
                {
                    return Ok(new
                    {
                        Message = "Configuration backed up successfully",
                        BackedUpAt = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new { Error = "Failed to backup configuration" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up configuration");
                return StatusCode(500, new { Error = "Internal server error during backup" });
            }
        }

        /// <summary>
        /// Restores the previous configuration from backup
        /// </summary>
        /// <returns>Restore operation result</returns>
        [HttpPost("restore")]
        public async Task<IActionResult> RestoreConfiguration()
        {
            try
            {
                var success = await _configurationUpdateService.RestorePreviousConfigurationAsync();
                
                if (success)
                {
                    _logger.LogInformation("Configuration restored from backup");
                    
                    return Ok(new
                    {
                        Message = "Configuration restored successfully from backup",
                        RestoredAt = DateTime.UtcNow,
                        RestartRecommended = true
                    });
                }
                else
                {
                    return NotFound(new { Error = "No backup configuration found to restore" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring configuration");
                return StatusCode(500, new { Error = "Internal server error during restore" });
            }
        }

        /// <summary>
        /// Gets the agent health status including configuration information
        /// </summary>
        /// <returns>Agent health status</returns>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealthStatus()
        {
            try
            {
                var configuration = await _configurationUpdateService.GetCurrentConfigurationAsync();
                
                var healthStatus = new
                {
                    AgentId = configuration.AgentId,
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Configuration = new
                    {
                        configuration.AgentVersion,
                        configuration.EventBatchSize,
                        configuration.EventFlushIntervalSeconds,
                        configuration.LogLevel,
                        configuration.EnableLocalAnalysis,
                        configuration.EnableEventFiltering
                    },
                    LastConfigurationUpdate = configuration.UpdatedAt,
                    MemoryUsageMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
                };

                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health status");
                return StatusCode(500, new { Error = "Internal server error" });
            }
        }
    }
}
