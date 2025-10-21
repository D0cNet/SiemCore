# SIEM Agent Deployment Guide

**Author:** Karega Anglin  
**Date:** October 12, 2025  
**Version:** 1.0

## Executive Summary

This deployment guide provides comprehensive instructions for installing, configuring, and managing the SIEM Agent across distributed network environments. The SIEM Agent is a lightweight, cross-platform service designed to collect security events from various sources and forward them to the central SiemCore application for analysis and correlation.

## 1. Deployment Architecture Overview

The SIEM Agent operates in a distributed architecture where multiple agents collect events from different network segments and forward them to a centralized SiemCore instance. This approach provides comprehensive visibility across the entire network infrastructure while maintaining scalability and resilience.

### 1.1 Network Topology

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   DMZ Segment   │    │  Internal LAN   │    │  Server Farm    │
│                 │    │                 │    │                 │
│ ┌─────────────┐ │    │ ┌─────────────┐ │    │ ┌─────────────┐ │
│ │ SIEM Agent  │ │    │ │ SIEM Agent  │ │    │ │ SIEM Agent  │ │
│ │   (DMZ-01)  │ │    │ │  (LAN-01)   │ │    │ │  (SRV-01)   │ │
│ └─────────────┘ │    │ └─────────────┘ │    │ └─────────────┘ │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                    ┌─────────────────────┐
                    │   Central SIEM      │
                    │                     │
                    │ ┌─────────────────┐ │
                    │ │   SiemCore      │ │
                    │ │   Application   │ │
                    │ └─────────────────┘ │
                    └─────────────────────┘
```

### 1.2 Communication Flow

The SIEM Agent maintains secure HTTPS communication with the SiemCore application, providing real-time event forwarding when connected and offline caching when the connection is unavailable. Each agent operates independently, ensuring that network issues in one segment do not affect monitoring in other areas.

## 2. System Requirements

### 2.1 Hardware Requirements

| Component | Minimum | Recommended | High-Volume |
|:---|:---|:---|:---|
| **CPU** | 1 core, 1 GHz | 2 cores, 2 GHz | 4+ cores, 3 GHz |
| **Memory** | 512 MB | 1 GB | 2+ GB |
| **Storage** | 1 GB | 5 GB | 20+ GB |
| **Network** | 1 Mbps | 10 Mbps | 100+ Mbps |

### 2.2 Software Requirements

**Operating System Support:**
- Windows Server 2019/2022
- Windows 10/11 (for workstation deployment)
- Ubuntu 20.04 LTS or later
- CentOS 8 or later
- Red Hat Enterprise Linux 8+
- SUSE Linux Enterprise Server 15+

**Runtime Dependencies:**
- .NET 8.0 Runtime (automatically installed with deployment package)
- SQLite (included with .NET runtime)
- Network connectivity to SiemCore (HTTPS/443)

### 2.3 Network Requirements

**Outbound Connectivity:**
- HTTPS (TCP/443) to SiemCore application
- DNS resolution for SiemCore hostname
- NTP synchronization for accurate timestamps

**Inbound Connectivity (Optional):**
- Syslog (UDP/514 or TCP/514) for network device logs
- Custom ports for specific collector configurations

## 3. Installation Procedures

### 3.1 Windows Installation

**Step 1: Download and Extract**
```powershell
# Download the Windows deployment package
Invoke-WebRequest -Uri "https://releases.siem.local/siem-agent-windows.zip" -OutFile "siem-agent.zip"

# Extract to installation directory
Expand-Archive -Path "siem-agent.zip" -DestinationPath "C:\Program Files\SiemAgent"
```

**Step 2: Configure the Agent**
```powershell
# Navigate to installation directory
cd "C:\Program Files\SiemAgent"

# Edit configuration file
notepad appsettings.json
```

**Step 3: Install as Windows Service**
```powershell
# Install the service
sc create "SiemAgent" binPath="C:\Program Files\SiemAgent\SiemAgent.exe" start=auto

# Configure service description
sc description "SiemAgent" "SIEM Agent for security event collection and forwarding"

# Start the service
sc start "SiemAgent"

# Verify service status
sc query "SiemAgent"
```

### 3.2 Linux Installation

**Step 1: Download and Install**
```bash
# Download the Linux deployment package
wget https://releases.siem.local/siem-agent-linux.tar.gz

# Extract to installation directory
sudo tar -xzf siem-agent-linux.tar.gz -C /opt/
sudo mv /opt/siem-agent-linux /opt/siem-agent

# Set permissions
sudo chown -R root:root /opt/siem-agent
sudo chmod +x /opt/siem-agent/SiemAgent
```

**Step 2: Create System User**
```bash
# Create dedicated user for the service
sudo useradd -r -s /bin/false -d /opt/siem-agent siem-agent
sudo chown -R siem-agent:siem-agent /opt/siem-agent
```

**Step 3: Configure Systemd Service**
```bash
# Create service file
sudo tee /etc/systemd/system/siem-agent.service > /dev/null <<EOF
[Unit]
Description=SIEM Agent
After=network.target

[Service]
Type=notify
User=siem-agent
Group=siem-agent
WorkingDirectory=/opt/siem-agent
ExecStart=/opt/siem-agent/SiemAgent
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=siem-agent

[Install]
WantedBy=multi-user.target
EOF

# Reload systemd and enable service
sudo systemctl daemon-reload
sudo systemctl enable siem-agent
sudo systemctl start siem-agent

# Verify service status
sudo systemctl status siem-agent
```

## 4. Configuration Management

### 4.1 Basic Configuration

The primary configuration file `appsettings.json` contains all agent settings:

```json
{
  "SiemCore": {
    "ApiUrl": "https://siem-core.company.com:5001",
    "ApiKey": "your-secure-api-key-here"
  },
  "Agent": {
    "AgentId": "unique-agent-identifier",
    "EventBatchSize": 100,
    "EventFlushIntervalSeconds": 30,
    "MaxRetryAttempts": 3,
    "MaxCachedEvents": 10000,
    "HealthCheckIntervalSeconds": 60,
    "ConfigurationRefreshIntervalSeconds": 300
  }
}
```

### 4.2 Collector Configuration

**File Log Collector Configuration:**
```json
{
  "Name": "System Log Collector",
  "Type": "FileLog",
  "Enabled": true,
  "CollectionIntervalSeconds": 60,
  "Settings": {
    "LogPaths": [
      "/var/log/syslog",
      "/var/log/auth.log",
      "/var/log/apache2/*.log",
      "C:\\Windows\\System32\\LogFiles\\W3SVC1\\*.log"
    ]
  },
  "IncludePatterns": ["ERROR", "WARN", "FAIL"],
  "ExcludePatterns": ["DEBUG", "TRACE"],
  "SeverityFilter": ""
}
```

**Windows Event Log Collector Configuration:**
```json
{
  "Name": "Security Event Collector",
  "Type": "WindowsEventLog",
  "Enabled": true,
  "CollectionIntervalSeconds": 30,
  "Settings": {
    "LogName": "Security",
    "Query": "*[System[Level<=3]]"
  },
  "IncludePatterns": [],
  "ExcludePatterns": [],
  "SeverityFilter": ""
}
```

**Syslog Collector Configuration:**
```json
{
  "Name": "Network Device Syslog",
  "Type": "Syslog",
  "Enabled": true,
  "CollectionIntervalSeconds": 1,
  "Settings": {
    "Port": 514,
    "Protocol": "UDP"
  },
  "IncludePatterns": [],
  "ExcludePatterns": ["DEBUG"],
  "SeverityFilter": ""
}
```

### 4.3 Environment-Specific Configuration

**Development Environment:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "SiemAgent": "Trace"
    }
  },
  "Agent": {
    "EventFlushIntervalSeconds": 10,
    "HealthCheckIntervalSeconds": 30
  }
}
```

**Production Environment:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SiemAgent": "Warning"
    }
  },
  "Agent": {
    "EventFlushIntervalSeconds": 30,
    "HealthCheckIntervalSeconds": 60,
    "MaxCachedEvents": 50000
  }
}
```

## 5. Security Configuration

### 5.1 API Key Management

**Generate Secure API Keys:**
```bash
# Generate a secure API key
openssl rand -base64 32
```

**Store API Keys Securely:**
- Use environment variables for sensitive configuration
- Implement key rotation procedures
- Restrict file permissions on configuration files

**Environment Variable Configuration:**
```bash
# Linux
export SIEM_CORE_API_KEY="your-secure-api-key"
export SIEM_CORE_API_URL="https://siem-core.company.com:5001"

# Windows
setx SIEM_CORE_API_KEY "your-secure-api-key"
setx SIEM_CORE_API_URL "https://siem-core.company.com:5001"
```

### 5.2 Network Security

**Firewall Configuration:**
```bash
# Linux (iptables)
sudo iptables -A OUTPUT -p tcp --dport 443 -d siem-core.company.com -j ACCEPT
sudo iptables -A INPUT -p udp --dport 514 -j ACCEPT

# Windows (PowerShell)
New-NetFirewallRule -DisplayName "SIEM Agent Outbound" -Direction Outbound -Protocol TCP -RemotePort 443
New-NetFirewallRule -DisplayName "SIEM Agent Syslog" -Direction Inbound -Protocol UDP -LocalPort 514
```

**TLS Configuration:**
- Ensure TLS 1.2 or higher is used for all communications
- Validate SSL certificates for SiemCore connections
- Implement certificate pinning for enhanced security

### 5.3 File System Security

**Linux Permissions:**
```bash
# Set secure permissions on configuration files
sudo chmod 600 /opt/siem-agent/appsettings.json
sudo chmod 600 /opt/siem-agent/events_cache.db

# Set directory permissions
sudo chmod 755 /opt/siem-agent
sudo chown -R siem-agent:siem-agent /opt/siem-agent
```

**Windows Permissions:**
```powershell
# Set file permissions using icacls
icacls "C:\Program Files\SiemAgent\appsettings.json" /grant "NT AUTHORITY\SYSTEM:F" /inheritance:r
icacls "C:\Program Files\SiemAgent\appsettings.json" /grant "Administrators:F"
```

## 6. Monitoring and Maintenance

### 6.1 Health Monitoring

**Service Status Monitoring:**
```bash
# Linux
sudo systemctl status siem-agent
journalctl -u siem-agent -f

# Windows
sc query "SiemAgent"
Get-EventLog -LogName Application -Source "SiemAgent" -Newest 50
```

**Performance Monitoring:**
```bash
# Monitor resource usage
top -p $(pgrep SiemAgent)
ps aux | grep SiemAgent

# Check disk usage for cache
du -sh /opt/siem-agent/events_cache.db
```

### 6.2 Log Management

**Log Rotation Configuration:**
```bash
# Create logrotate configuration
sudo tee /etc/logrotate.d/siem-agent > /dev/null <<EOF
/var/log/siem-agent/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 644 siem-agent siem-agent
    postrotate
        systemctl reload siem-agent
    endscript
}
EOF
```

### 6.3 Backup and Recovery

**Configuration Backup:**
```bash
# Create configuration backup
sudo tar -czf siem-agent-config-$(date +%Y%m%d).tar.gz /opt/siem-agent/appsettings.json

# Backup cache database
sudo cp /opt/siem-agent/events_cache.db /backup/siem-agent-cache-$(date +%Y%m%d).db
```

**Disaster Recovery Procedures:**
1. Restore configuration from backup
2. Restart the SIEM Agent service
3. Verify connectivity to SiemCore
4. Monitor for successful event forwarding

## 7. Troubleshooting

### 7.1 Common Issues

**Connection Problems:**
```bash
# Test connectivity to SiemCore
curl -k https://siem-core.company.com:5001/health

# Check DNS resolution
nslookup siem-core.company.com

# Verify certificate validity
openssl s_client -connect siem-core.company.com:443 -servername siem-core.company.com
```

**Performance Issues:**
```bash
# Check system resources
free -h
df -h
iostat 1 5

# Monitor network usage
iftop -i eth0
netstat -i
```

**Event Collection Issues:**
```bash
# Verify file permissions
ls -la /var/log/
sudo -u siem-agent cat /var/log/syslog

# Test log file accessibility
sudo -u siem-agent tail -f /var/log/auth.log
```

### 7.2 Diagnostic Commands

**Agent Status Check:**
```bash
# Comprehensive status check
sudo systemctl status siem-agent --no-pager -l

# Check recent logs
journalctl -u siem-agent --since "1 hour ago" --no-pager

# Verify configuration
sudo -u siem-agent /opt/siem-agent/SiemAgent --validate-config
```

**Network Diagnostics:**
```bash
# Test HTTPS connectivity
curl -v -k https://siem-core.company.com:5001/health

# Check listening ports
sudo netstat -tlnp | grep SiemAgent

# Monitor network traffic
sudo tcpdump -i any host siem-core.company.com
```

## 8. Scaling and Performance Optimization

### 8.1 High-Volume Environments

**Configuration Optimization:**
```json
{
  "Agent": {
    "EventBatchSize": 500,
    "EventFlushIntervalSeconds": 15,
    "MaxCachedEvents": 100000,
    "HealthCheckIntervalSeconds": 300
  }
}
```

**System Optimization:**
```bash
# Increase file descriptor limits
echo "siem-agent soft nofile 65536" >> /etc/security/limits.conf
echo "siem-agent hard nofile 65536" >> /etc/security/limits.conf

# Optimize network buffers
echo 'net.core.rmem_max = 16777216' >> /etc/sysctl.conf
echo 'net.core.wmem_max = 16777216' >> /etc/sysctl.conf
```

### 8.2 Load Balancing

**Multiple SiemCore Instances:**
```json
{
  "SiemCore": {
    "ApiUrl": "https://siem-core-lb.company.com:5001",
    "BackupUrls": [
      "https://siem-core-01.company.com:5001",
      "https://siem-core-02.company.com:5001"
    ]
  }
}
```

## 9. Compliance and Auditing

### 9.1 Audit Trail

The SIEM Agent maintains comprehensive audit trails for compliance requirements:

- **Event Processing Logs**: All collected and forwarded events
- **Configuration Changes**: Timestamped configuration modifications
- **Health Status**: Regular health check results and system metrics
- **Error Tracking**: Detailed error logs with stack traces and context

### 9.2 Compliance Features

**Data Retention:**
- Configurable cache retention periods
- Automatic cleanup of expired events
- Compliance with data protection regulations

**Integrity Verification:**
- Event checksums for data integrity
- Secure transmission protocols
- Audit trail for all agent operations

## 10. Conclusion

The SIEM Agent provides a robust, scalable solution for distributed security event collection. Proper deployment and configuration ensure comprehensive monitoring coverage while maintaining system performance and security. Regular monitoring and maintenance procedures help maintain optimal operation and early detection of potential issues.

For additional support and advanced configuration options, consult the technical documentation or contact your SIEM administrator.

---

*This deployment guide provides comprehensive instructions for enterprise-grade SIEM Agent deployment. Follow security best practices and organizational policies when implementing these procedures.*
