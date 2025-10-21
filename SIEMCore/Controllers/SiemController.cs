using Microsoft.AspNetCore.Mvc;
using SiemCore.Models;
using SiemCore.Services;

namespace SiemCore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SiemController : ControllerBase
    {
        private readonly IEventProcessingService _eventProcessingService;
        private readonly IAlertService _alertService;
        private readonly ICorrelationService _correlationService;
        private readonly ILogger<SiemController> _logger;

        public SiemController(
            IEventProcessingService eventProcessingService,
            IAlertService alertService,
            ICorrelationService correlationService,
            ILogger<SiemController> logger)
        {
            _eventProcessingService = eventProcessingService;
            _alertService = alertService;
            _correlationService = correlationService;
            _logger = logger;
        }

        /// <summary>
        /// Ingests a new security event into the SIEM system
        /// </summary>
        [HttpPost("events")]
        public async Task<IActionResult> IngestEvent([FromBody] SiemEvent siemEvent)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var processedEvent = await _eventProcessingService.ProcessEventAsync(siemEvent);
                
                // Trigger correlation analysis
                await _correlationService.AnalyzeEventAsync(processedEvent);
                
                _logger.LogInformation($"Event {processedEvent.Id} ingested successfully");
                
                return Ok(new { EventId = processedEvent.Id, Status = "Processed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting event");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves events based on search criteria
        /// </summary>
        [HttpGet("events")]
        public async Task<IActionResult> GetEvents(
            [FromQuery] DateTime? startTime,
            [FromQuery] DateTime? endTime,
            [FromQuery] string? sourceSystem,
            [FromQuery] string? eventType,
            [FromQuery] string? severity,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                var events = await _eventProcessingService.SearchEventsAsync(
                    startTime, endTime, sourceSystem, eventType, severity, page, pageSize);
                
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves all active alerts
        /// </summary>
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] AlertStatus? status,
            [FromQuery] AlertSeverity? severity,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var alerts = await _alertService.GetAlertsAsync(status, severity, page, pageSize);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alerts");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates an alert status
        /// </summary>
        [HttpPut("alerts/{alertId}/status")]
        public async Task<IActionResult> UpdateAlertStatus(Guid alertId, [FromBody] AlertStatusUpdate statusUpdate)
        {
            try
            {
                var success = await _alertService.UpdateAlertStatusAsync(alertId, statusUpdate.Status, statusUpdate.Resolution);
                
                if (success)
                {
                    return Ok(new { Message = "Alert status updated successfully" });
                }
                
                return NotFound(new { Message = "Alert not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating alert {alertId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets SIEM dashboard statistics
        /// </summary>
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var stats = await _eventProcessingService.GetDashboardStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard stats");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class AlertStatusUpdate
    {
        public AlertStatus Status { get; set; }
        public string Resolution { get; set; } = string.Empty;
    }
}
