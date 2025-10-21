using SiemAgent.Models;

namespace SiemAgent.Services
{
    /// <summary>
    /// Interface for monitoring agent health and performance
    /// </summary>
    public interface IAgentHealthService
    {
        Task<AgentHealth> GetHealthStatusAsync();
        
        Task UpdateEventStatisticsAsync(long collected, long forwarded, long cached, long filtered);
        
        Task RecordErrorAsync(string error);
        
        Task RecordWarningAsync(string warning);
        
        Task ClearErrorsAndWarningsAsync();
        
        Task<double> GetCpuUsageAsync();
        
        Task<long> GetMemoryUsageAsync();
        
        Task<long> GetDiskUsageAsync();
        
        void SetConnectionStatus(bool isConnected);
        
        void SetConfigurationUpdateTime(DateTime updateTime);
        
        DateTime StartTime { get; }
    }
}
