# Monitoring System Client Service

A professional-grade .NET 9 background service for collecting and transmitting system metrics to a monitoring server.

## Architecture

### Project Structure

```
Monitoring-system-client-service/
??? CommandHandling/        # CLI command handling
??? Configuration/          # Configuration models and interfaces
??? Extensions/             # Dependency injection extensions
??? Logging/                # Logging abstractions
??? Models/                 # Data models (TOML configuration, metrics, etc.)
??? Results/                # Operation result types for error handling
??? Services/               # Business logic services
??? Validation/             # Input validation and environment checks
??? Worker.cs               # Background service worker
??? Program.cs              # Application entry point
```

### Key Components

#### Program.cs
- **Responsibility**: Application bootstrap and orchestration
- **Features**:
  - Platform validation (Linux only)
  - Two execution modes: command-line and background service
  - Proper error handling and logging
  - Configuration management

#### Worker.cs
- **Responsibility**: Background metrics collection and transmission
- **Features**:
  - Periodic metric collection
  - Async/await patterns
  - Graceful cancellation support
  - Comprehensive logging

#### SetupService.cs
- **Responsibility**: Device setup and configuration
- **Features**:
  - Device registration
  - API key regeneration
  - Configuration file management
  - Support for multiple setup modes

#### ApiClientService.cs
- **Responsibility**: HTTP communication with monitoring server
- **Features**:
  - Login and token management
  - Metric submission
  - Device management
  - Comprehensive error logging

#### CommandHandler.cs
- **Responsibility**: CLI command routing and execution
- **Features**:
  - Command parsing
  - Help/usage information
  - Error handling
  - Support for multiple commands

### Dependency Injection

Services are registered using Microsoft.Extensions.DependencyInjection with two profiles:

#### Monitoring Services (Production)
```csharp
services.AddMonitoringServices()
  // Includes: ApiClientService, LinuxMetricsService, Worker
```

#### Setup Services (CLI Operations)
```csharp
services.AddSetupServices()
  // Includes: ApiClientService, SetupService, CommandHandler
```

## Usage

### Running the Service

```bash
# Start the monitoring service (requires configuration)
sudo systemctl start monitoring-agent

# View logs
sudo journalctl -u monitoring-agent -f
```

### Commands

#### Setup / Login
Regenerate API key and configure the service:

```bash
# Using email and password
./MonitoringAgent setup \
  --server-url=https://api.example.com \
  --email=user@example.com \
  --password=secretpassword \
  --device-id=550e8400-e29b-41d4-a716-446655440000 \
  --interval=10

# Using existing API key
./MonitoringAgent setup \
  --server-url=https://api.example.com \
  --device-id=550e8400-e29b-41d4-a716-446655440000 \
  --api-key=your-api-key-here \
  --interval=10
```

#### Register
Register a new device:

```bash
./MonitoringAgent register \
  --server-url=https://api.example.com \
  --email=user@example.com \
  --password=secretpassword \
  --device-name=MyServer \
  --interval=10
```

#### Print Configuration
Display current configuration:

```bash
./MonitoringAgent print-config
```

#### Help
Show available commands:

```bash
./MonitoringAgent help
```

## Configuration

Configuration is stored in `agent_config.toml`:

```toml
# Monitoring Agent Configuration
# Generated at [timestamp]

BaseUrl = "https://api.example.com"
DeviceId = "550e8400-e29b-41d4-a716-446655440000"
ApiKey = "your-api-key-here"
IntervalSeconds = 10
```

## Logging

### Console Output (Commands)
- `[INFO]` - Important operations
- `[DEBUG]` - Detailed debugging information
- `[ERROR]` - Error conditions

### Service Logging (Background Service)
Uses Microsoft.Extensions.Logging with:
- Console provider
- Systemd console provider (for journalctl integration)

## Error Handling

The application implements comprehensive error handling:

1. **Platform Validation**: Ensures the service runs only on Linux
2. **Configuration Validation**: Validates GUID format, required fields
3. **Network Error Handling**: Graceful fallbacks for failed requests
4. **Cancellation Handling**: Proper cleanup on graceful shutdown


## Requirements

- .NET 9.0 or higher
- Linux operating system
- Access to monitoring server API
- Valid API credentials or API key

