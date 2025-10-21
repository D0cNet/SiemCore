using SiemCore.Models;

namespace SiemCore.Services
{
    /// <summary>
    /// Service interface for managing SIEM agent configurations
    /// </summary>
    public interface IAgentConfigurationService
    {
        /// <summary>
        /// Gets the current configuration for a specific agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <returns>The agent configuration or null if not found</returns>
        Task<AgentConfiguration?> GetAgentConfigurationAsync(string agentId);

        /// <summary>
        /// Updates the configuration for a specific agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <param name="configuration">The new configuration to apply</param>
        /// <returns>True if the configuration was updated successfully</returns>
        Task<bool> UpdateAgentConfigurationAsync(string agentId, AgentConfiguration configuration);

        /// <summary>
        /// Gets the default configuration template for new agents
        /// </summary>
        /// <returns>The default agent configuration</returns>
        Task<AgentConfiguration> GetDefaultConfigurationAsync();

        /// <summary>
        /// Updates the default configuration template
        /// </summary>
        /// <param name="configuration">The new default configuration</param>
        /// <returns>True if the default configuration was updated successfully</returns>
        Task<bool> UpdateDefaultConfigurationAsync(AgentConfiguration configuration);

        /// <summary>
        /// Gets all registered agent configurations
        /// </summary>
        /// <returns>List of all agent configurations</returns>
        Task<IEnumerable<AgentConfiguration>> GetAllAgentConfigurationsAsync();

        /// <summary>
        /// Registers a new agent with default configuration
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <param name="agentInfo">Additional agent information</param>
        /// <returns>The initial configuration for the agent</returns>
        Task<AgentConfiguration> RegisterAgentAsync(string agentId, AgentRegistrationInfo agentInfo);

        /// <summary>
        /// Removes an agent configuration
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <returns>True if the agent was removed successfully</returns>
        Task<bool> RemoveAgentAsync(string agentId);

        /// <summary>
        /// Pushes updated configuration to a specific agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <param name="configuration">The configuration to push</param>
        /// <returns>True if the configuration was pushed successfully</returns>
        Task<bool> PushConfigurationToAgentAsync(string agentId, AgentConfiguration configuration);

        /// <summary>
        /// Pushes updated configuration to all agents
        /// </summary>
        /// <param name="configuration">The configuration to push to all agents</param>
        /// <returns>Dictionary of agent IDs and their push results</returns>
        Task<Dictionary<string, bool>> PushConfigurationToAllAgentsAsync(AgentConfiguration configuration);

        /// <summary>
        /// Gets the configuration update history for an agent
        /// </summary>
        /// <param name="agentId">The unique identifier of the agent</param>
        /// <returns>List of configuration updates</returns>
        Task<IEnumerable<ConfigurationUpdateHistory>> GetConfigurationHistoryAsync(string agentId);

        /// <summary>
        /// Validates a configuration before applying it
        /// </summary>
        /// <param name="configuration">The configuration to validate</param>
        /// <returns>Validation result with any errors</returns>
        Task<ConfigurationValidationResult> ValidateConfigurationAsync(AgentConfiguration configuration);
    }

    /// <summary>
    /// Agent registration information
    /// </summary>
    public class AgentRegistrationInfo
    {
        public string AgentVersion { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Configuration update history entry
    /// </summary>
    public class ConfigurationUpdateHistory
    {
        public Guid Id { get; set; }
        public string AgentId { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public string ChangeDescription { get; set; } = string.Empty;
        public AgentConfiguration? PreviousConfiguration { get; set; }
        public AgentConfiguration? NewConfiguration { get; set; }
        public bool PushSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Configuration validation result
    /// </summary>
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
