# SIEM Agent - Distributed Security Event Collection Service

A lightweight, cross-platform SIEM agent designed to collect, process, and forward security events to the central SiemCore application. This agent enables distributed security monitoring across network devices and target machines.

## 🚀 Features

- **Multi-Platform Support**: Runs on Windows and Linux systems
- **Multiple Event Collectors**: File logs, Windows Event Logs, and Syslog
- **Offline Caching**: SQLite-based local storage when SIEM Core is unavailable
- **Real-time Forwarding**: Immediate event transmission when connected
- **Health Monitoring**: Comprehensive agent health reporting and metrics
- **Remote Configuration**: Centralized configuration management from SIEM Core
- **Intelligent Filtering**: Configurable include/exclude patterns and severity filters
- **Retry Logic**: Automatic retry with exponential backoff for failed transmissions
- **Resource Monitoring**: CPU, memory, and disk usage tracking

## 🏗️ Architecture

The SIEM Agent follows a modular architecture with the following components:

- **Event Collectors**: Pluggable modules for different data sources
- **Event Cache Service**: SQLite-based offline storage
- **Event Forwarder Service**: HTTP-based communication with SIEM Core
- **Health Service**: System monitoring and performance tracking
- **Worker Service**: Main orchestration and lifecycle management

## 📋 Prerequisites

- .NET 8.0 Runtime
- SQLite (included with .NET)
- Network connectivity to SIEM Core (for real-time forwarding)

## 🚀 Quick Start

### 1. Configuration

Edit `appsettings.json` to configure the agent:

```json
{
  "SiemCore": {
    "ApiUrl": "https://your-siem-core-server:5001",
    "ApiKey": "your-api-key-here"
  },
  "Agent": {
    "AgentId": "unique-agent-identifier",
    "EventBatchSize": 100,
    "EventFlushIntervalSeconds": 30
  }
}
```

### 2. Run the Agent

```bash
# Development mode
dotnet run

# Production deployment
dotnet publish -c Release
./bin/Release/net8.0/SiemAgent
```

### 3. Install as Service

#### Windows Service
```powershell
# Install as Windows Service
sc create "SiemAgent" binPath="C:\path\to\SiemAgent.exe"
sc start "SiemAgent"
```

#### Linux Systemd Service
```bash
# Create service file
sudo nano /etc/systemd/system/siem-agent.service

# Enable and start service
sudo systemctl enable siem-agent
sudo systemctl start siem-agent
```

## 🔧 Configuration

### Agent Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `AgentId` | Unique identifier for the agent | Machine name |
| `SiemCoreApiUrl` | URL of the SIEM Core API | `https://localhost:5001` |
| `ApiKey` | Authentication key for SIEM Core | Empty |
| `EventBatchSize` | Number of events to send in batch | 100 |
| `EventFlushIntervalSeconds` | Interval for flushing cached events | 30 |
| `MaxRetryAttempts` | Maximum retry attempts for failed events | 3 |
| `MaxCachedEvents` | Maximum events to cache offline | 10000 |

### Collector Configuration

#### File Log Collector
```json
{
  "Name": "File Log Collector",
  "Type": "FileLog",
  "Enabled": true,
  "Settings": {
    "LogPaths": [
      "/var/log/*.log",
      "/var/log/syslog",
      "C:\\Windows\\System32\\LogFiles\\*.log"
    ]
  },
  "ExcludePatterns": ["DEBUG", "TRACE"]
}
```

#### Windows Event Log Collector
```json
{
  "Name": "Windows Event Log Collector",
  "Type": "WindowsEventLog",
  "Enabled": true,
  "Settings": {
    "LogName": "Security",
    "Query": "*[System[Level<=3]]"
  }
}
```

#### Syslog Collector
```json
{
  "Name": "Syslog Collector",
  "Type": "Syslog",
  "Enabled": true,
  "Settings": {
    "Port": 514,
    "Protocol": "UDP"
  }
}
```

## 📊 Monitoring

### Health Status

The agent provides comprehensive health monitoring:

- **Connection Status**: Real-time connection to SIEM Core
- **Event Statistics**: Collected, forwarded, cached, and filtered counts
- **Resource Usage**: CPU, memory, and disk utilization
- **Collector Status**: Individual collector health and performance
- **Error Tracking**: Recent errors and warnings with timestamps

### Logging

The agent uses structured logging with configurable levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SiemAgent": "Debug"
    }
  }
}
```

## 🔒 Security Features

- **TLS Encryption**: All communication with SIEM Core uses HTTPS
- **API Key Authentication**: Secure authentication with the central server
- **Local Data Protection**: Cached events stored securely in SQLite
- **Input Validation**: Comprehensive validation of configuration and events
- **Error Handling**: Graceful handling of failures without data loss

## 🛠️ Development

### Building from Source

```bash
git clone <repository-url>
cd SiemAgent
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Adding Custom Collectors

1. Implement the `IEventCollector` interface
2. Register the collector in `Program.cs`
3. Add configuration schema to `CollectorConfiguration`

Example:
```csharp
public class CustomCollector : IEventCollector
{
    public string Name => "Custom Collector";
    public string Type => "Custom";
    
    public async Task<bool> InitializeAsync()
    {
        // Initialize collector
        return true;
    }
    
    public async Task<IEnumerable<SiemEvent>> CollectEventsAsync()
    {
        // Collect and return events
        return events;
    }
}
```

## 📈 Performance

### Resource Usage

- **Memory**: ~50-100 MB typical usage
- **CPU**: <5% on modern systems during normal operation
- **Disk**: Minimal footprint, cache size configurable
- **Network**: Efficient batching reduces bandwidth usage

### Scalability

- **Event Throughput**: 1000+ events/second per collector
- **Concurrent Collectors**: Multiple collectors run independently
- **Cache Capacity**: Configurable up to millions of events
- **Network Resilience**: Automatic reconnection and retry logic

## 🐛 Troubleshooting

### Common Issues

1. **Connection Failed**
   - Verify SIEM Core URL and API key
   - Check network connectivity and firewall rules
   - Review TLS certificate configuration

2. **High Memory Usage**
   - Reduce `MaxCachedEvents` setting
   - Check for log file growth patterns
   - Monitor collector performance

3. **Missing Events**
   - Verify collector configuration and file paths
   - Check include/exclude patterns
   - Review severity filters

### Log Analysis

```bash
# View recent logs
journalctl -u siem-agent -f

# Check specific time range
journalctl -u siem-agent --since "2024-01-01 00:00:00"
```

## 🔄 Updates

The agent supports remote updates through the SIEM Core:

1. New version deployed to SIEM Core
2. Agent receives update notification
3. Automatic download and installation
4. Graceful restart with configuration preservation

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For support and questions:
- Check the troubleshooting section above
- Review agent logs for error details
- Verify configuration against this documentation
- Contact your SIEM administrator for API key issues

## 🔮 Roadmap

- [ ] Additional collector types (Database, API, Network)
- [ ] Advanced local analysis and filtering
- [ ] Compression for network transmission
- [ ] Enhanced security with certificate-based authentication
- [ ] Web-based local management interface
- [ ] Performance optimization for high-volume environments

---

**Note**: This agent is designed to work with the SiemCore application. Ensure both components are properly configured and compatible versions are deployed.
