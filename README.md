<p align="center">
  <svg width="64" height="64" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg">
    <rect width="64" height="64" rx="16" fill="#111113"/>
    <rect x="12" y="16" width="40" height="26" rx="3" stroke="#F4F4F5" stroke-width="4"/>
    <path d="M32 42V50M24 50H40" stroke="#F4F4F5" stroke-width="4" stroke-linecap="round"/>
    <path d="M18 31H24L28 23L34 37L38 31H46" stroke="#F4F4F5" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
  </svg>

  <h1 align="center">Monitoring System — Client Agent</h1>
  <p align="center">
    Lightweight Linux daemon for real-time system metrics collection and reporting.
    <br />
    Part of the <strong>Aplikace pro sběr, monitoring a vizualizaci dat počítačů a serverů</strong> project.
  </p>
</p>

<p align="center">
  <a href="README-CS.md">🇨🇿 Česká verze</a>
</p>



## About

The **Monitoring System Client Agent** is a .NET 9 Worker Service designed to run as a background daemon on Linux machines. It continuously collects system metrics and sends them to the central backend server (Spring Boot), where they are displayed in real-time dashboard graphs on the frontend (Vite + React).

The agent operates in two modes:

| Mode | Description |
|---|---|
| **CLI Mode** | Interactive commands for initial device setup, registration, and configuration management. |
| **Daemon Mode** | Background service that periodically collects and submits system metrics via the REST API. |

### Collected Metrics

| Metric | Source |
|---|---|
| CPU Usage (%) | `/proc/stat` |
| CPU Frequency (MHz) | `/proc/cpuinfo` |
| CPU Temperature (°C) | `/sys/class/thermal/` |
| RAM Usage (MB) | `/proc/meminfo` |
| Disk Usage (%) | Root filesystem |
| Network In/Out (kbps) | `/proc/net/dev` |
| Process Count | System process list |
| TCP Connections | Active TCP connections |
| Listening Ports | TCP listeners |
| System Uptime (s) | `/proc/uptime` |

---

## Features

- **Automatic device detection** — IP address, MAC address, hardware model, OS, and SSH status detected during registration
- **Flexible configuration** — CLI flags, TOML configuration file, or a combination of both
- **Self-signed certificate support** — connect to backend servers using self-signed SSL certificates
- **Systemd integration** — native `journald` logging and proper service lifecycle management
- **Configurable collection interval** — adjust how frequently metrics are sampled and submitted

---

## Quick Start

### Prerequisites

- A Linux machine (x64 or ARM64)
- An account on the Monitoring System backend server
- Network access to the backend API

### 1. Download

Download the latest standalone binary from the [Releases](../../releases) page for your architecture.

```bash
# Make the binary executable
chmod +x monitoring-agent
```

### 2. Configure the Agent

#### Option A: Copy the Setup Command from the Dashboard (Easiest)

When you create a new device or regenerate an API key in the web dashboard, the application displays a ready-to-use **setup command**. Simply copy it and paste it into the terminal:

```bash
./monitoring-agent setup \
  --server-url=https://your-server.com \
  --device-id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx \
  --api-key=your-generated-api-key
```

> **Note:** The binary filename in the command may differ from your downloaded file. Ensure your terminal is in the correct directory where the binary was downloaded or installed before running the command (e.g. `cd /opt/monitoring-agent`).

#### Option B: Register a New Device via CLI

If you prefer to register the device directly from the terminal (automatically detects hardware info):

```bash
./monitoring-agent register \
  --server-url=https://your-server.com \
  --email=your@email.com \
  --password=yourpassword \
  --device-name="My Server"
```

#### Option C: Set Up with Credentials (Existing Device)

Regenerates the API key for an already-registered device using your account credentials:

```bash
./monitoring-agent setup \
  --server-url=https://your-server.com \
  --email=your@email.com \
  --password=yourpassword \
  --device-id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

### 3. Verify Configuration

```bash
./monitoring-agent print-config
```

---

<br>

## Configuration

After setup, the agent stores its configuration in `agent_config.toml` in the working directory:

```toml
# Monitoring Agent Configuration

base_url = "https://your-server.com"
device_id = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
api_key = "your-api-key"
interval_seconds = 10
allow_self_signed_certificates = true
```

### Configuration Options

| Key | CLI Flag | Default | Description |
|---|---|---|---|
| `base_url` | `--server-url` | `https://localhost:8080` | Backend server URL |
| `device_id` | `--device-id` | — | Device UUID assigned during registration |
| `api_key` | `--api-key` | — | API key for authenticating metric submissions |
| `interval_seconds` | `--interval` | `10` | Metrics collection interval in seconds |
| `allow_self_signed_certificates` | `--allow-self-signed-certificates` | `true` | Accept self-signed SSL certificates |

---

## CLI Reference

```
Monitoring System Client Service

USAGE:
  <command> [options]

COMMANDS:
  setup          Initialize client configuration
  register       Register a new device
  print-config   Display current configuration
  help           Show this message

SETUP OPTIONS:
  --interval=<seconds>                           Metrics collection interval (default: 10).
  --allow-self-signed-certificates=<true|false>  Allow self-signed SSL certificates (default: true).
```

---

## Systemd Daemon Setup

### Option A: Automated Installation

An installation script is provided for convenience:

```bash
sudo bash install.sh
```

This copies the binary to `/opt/monitoring-agent/`, installs the systemd service, and starts the daemon.

### Option B: Manual Installation

#### 1. Copy the Binary

```bash
sudo mkdir -p /opt/monitoring-agent
sudo cp monitoring-agent /opt/monitoring-agent/
sudo chmod +x /opt/monitoring-agent/monitoring-agent
```

#### 2. Run Setup

```bash
cd /opt/monitoring-agent
sudo ./monitoring-agent register \
  --server-url=https://your-server.com \
  --email=your@email.com \
  --password=yourpassword \
  --device-name="My Server"
```

#### 3. Create the Systemd Service File

Create `/etc/systemd/system/monitoring-agent.service`:

```ini
[Unit]
Description=Monitoring System Client Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/opt/monitoring-agent/monitoring-agent
WorkingDirectory=/opt/monitoring-agent
SyslogIdentifier=monitoring-agent
StandardOutput=journal
StandardError=journal
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

#### 4. Enable and Start the Service

```bash
sudo systemctl daemon-reload
sudo systemctl enable monitoring-agent.service
sudo systemctl start monitoring-agent.service
```

#### 5. Check Status and Logs

```bash
# Service status
sudo systemctl status monitoring-agent.service

# Live logs
sudo journalctl -u monitoring-agent.service -f
```

---


## Building from Source

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build

```bash
dotnet build
```

### Publish as Standalone Binary

```bash
# For x64 Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# For ARM64 Linux 
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true
```

The output binary will be located in `bin/Release/net9.0/<runtime>/publish/`.

---

## Project Structure

```
Monitoring-system-client-service/
├── Program.cs                  # Entry point — CLI/daemon routing
├── Worker.cs                   # Background service — metrics loop
├── CommandHandling/
│   ├── CommandHandler.cs       # CLI command dispatcher
│   └── CliParser.cs            # --key=value argument parser
├── Configuration/
│   └── ConfigService.cs        # TOML config read/write
├── Models/
│   ├── ConfigModel.cs          # Configuration schema
│   ├── MetricsModel.cs         # Collected metrics DTO
│   ├── CreateDeviceModel.cs    # Device registration payload
│   ├── LoginModel.cs           # Auth response
│   └── DeviceWithApiKeyModel.cs
├── Services/
│   ├── ApiClientService.cs     # HTTP client for backend API
│   ├── LinuxMetricsService.cs  # /proc & /sys metrics collector
│   ├── SetupService.cs         # Setup & registration logic
│   └── SystemInfoService.cs    # Hardware/network detection
└── agent_config.toml           # Generated configuration file
```
