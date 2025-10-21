using SiemAgent.Models;

namespace SiemAgent.Services
{
    /// <summary>
    /// Interface for caching events when the SIEM Core is unavailable
    /// </summary>
    public interface IEventCacheService
    {
        Task<bool> CacheEventAsync(SiemEvent siemEvent);
        
        Task<IEnumerable<SiemEvent>> GetCachedEventsAsync(int batchSize = 100);
        
        Task<bool> RemoveCachedEventAsync(Guid eventId);
        
        Task<bool> RemoveCachedEventsAsync(IEnumerable<Guid> eventIds);
        
        Task<int> GetCachedEventCountAsync();
        
        Task<bool> ClearCacheAsync();
        
        Task<bool> InitializeAsync();
        
        Task<bool> CleanupExpiredEventsAsync(TimeSpan maxAge);
    }
}
