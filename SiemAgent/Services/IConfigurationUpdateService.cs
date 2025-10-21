using SiemAgent.Models;

namespace SiemAgent.Services
{
    /// <summary>
    /// Service interface for handling configuration updates from SiemCore
    /// </summary>
    public interface IConfigurationUpdateService
    {
        /// <summary>
        /// Applies a new configuration to the agent
        /// </summary>
        /// <param name="newConfiguration">The new configuration to apply</param>
        /// <returns>True if the configuration was applied successfully</returns>
        Task<bool> ApplyConfigurationAsync(AgentConfiguration newConfiguration);

        /// <summary>
        /// Validates a configuration before applying it
        /// </summary>
        /// <param name="configuration">The configuration to validate</param>
        /// <returns>Validation result</returns>
        Task<ConfigurationValidationResult> ValidateConfigurationAsync(AgentConfiguration configuration);

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        /// <returns>The current agent configuration</returns>
        Task<AgentConfiguration> GetCurrentConfigurationAsync();

        /// <summary>
        /// Backs up the current configuration before applying a new one
        /// </summary>
        /// <returns>True if backup was successful</returns>
        Task<bool> BackupCurrentConfigurationAsync();

        /// <summary>
        /// Restores the previous configuration in case of failure
        /// </summary>
        /// <returns>True if restore was successful</returns>
        Task<bool> RestorePreviousConfigurationAsync();

        /// <summary>
        /// Event fired when configuration is updated
        /// </summary>
        event EventHandler<ConfigurationUpdatedEventArgs>? ConfigurationUpdated;
    }

    /// <summary>
    /// Event arguments for configuration update events
    /// </summary>
    public class ConfigurationUpdatedEventArgs : EventArgs
    {
        public AgentConfiguration? PreviousConfiguration { get; set; }
        public AgentConfiguration NewConfiguration { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string UpdateSource { get; set; } = string.Empty;
        public bool RestartRequired { get; set; }
    }

    /// <summary>
    /// Configuration validation result
    /// </summary>
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool RestartRequired { get; set; }
    }
}
