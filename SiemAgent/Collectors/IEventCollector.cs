using SiemAgent.Models;

namespace SiemAgent.Collectors
{
    /// <summary>
    /// Interface for event collectors that gather security events from various sources
    /// </summary>
    public interface IEventCollector
    {
        string Name { get; }
        
        string Type { get; }
        
        bool IsEnabled { get; set; }
        
        CollectorConfiguration Configuration { get; set; }
        
        Task<bool> InitializeAsync();
        
        Task<IEnumerable<SiemEvent>> CollectEventsAsync();
        
        Task<CollectorHealth> GetHealthStatusAsync();
        
        Task StopAsync();
        
        event EventHandler<SiemEvent> EventCollected;
        
        event EventHandler<string> ErrorOccurred;

        void Dispose();
    }
}
