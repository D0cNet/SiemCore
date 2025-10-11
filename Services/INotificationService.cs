using SiemCore.Models;

namespace SiemCore.Services
{
    /// <summary>
    /// Interface for notification service
    /// </summary>
    public interface INotificationService
    {
        Task SendAlertNotificationAsync(Alert alert);
        Task SendEmailNotificationAsync(string to, string subject, string body);
        Task SendSlackNotificationAsync(string channel, string message);
        Task SendSmsNotificationAsync(string phoneNumber, string message);
        Task<bool> ConfigureNotificationChannelAsync(NotificationChannel channel);
    }

    public class NotificationChannel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public NotificationChannelType Type { get; set; }
        public Dictionary<string, string> Configuration { get; set; } = new Dictionary<string, string>();
        public bool IsEnabled { get; set; } = true;
        public List<AlertSeverity> SeverityFilters { get; set; } = new List<AlertSeverity>();
    }

    public enum NotificationChannelType
    {
        Email,
        Slack,
        SMS,
        Webhook,
        Teams
    }
}
