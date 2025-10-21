using SiemAgent.Models;

namespace SiemAgent.Services
{
    /// <summary>
    /// Interface for forwarding events to the SIEM Core
    /// </summary>
    public interface IEventForwarderService
    {
        Task<bool> ForwardEventAsync(SiemEvent siemEvent);
        
        Task<bool> ForwardEventsAsync(IEnumerable<SiemEvent> siemEvents);
        
        Task<bool> TestConnectionAsync();
        
        Task<bool> SendHealthStatusAsync(AgentHealth healthStatus);
        
        Task<AgentConfiguration?> GetConfigurationAsync();
        
        bool IsConnected { get; }
        
        event EventHandler<bool> ConnectionStatusChanged;
    }
}
