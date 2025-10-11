using SiemCore.Models;

namespace SiemCore.Services
{
    /// <summary>
    /// Interface for event processing service
    /// </summary>
    public interface IEventProcessingService
    {
        Task<SiemEvent> ProcessEventAsync(SiemEvent siemEvent);
        Task<IEnumerable<SiemEvent>> SearchEventsAsync(
            DateTime? startTime, 
            DateTime? endTime, 
            string? sourceSystem, 
            string? eventType, 
            string? severity, 
            int page, 
            int pageSize);
        Task<object> GetDashboardStatsAsync();
        Task<bool> EnrichEventAsync(SiemEvent siemEvent);
        Task<bool> NormalizeEventAsync(SiemEvent siemEvent);
    }
}
