using Monitoring_system_client_service.Configuration;
using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service.CommandHandling;

/// <summary>
/// Handles command-line interface operations for device setup, registration, and configuration management.
/// </summary>
public class CommandHandler
{
    private readonly SetupService _setupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandler"/> class.
    /// </summary>
    /// <param name="setupService">The setup service for handling configuration commands.</param>
    public CommandHandler(SetupService setupService)
    {
        _setupService = setupService;
    }

    /// <summary>
    /// Executes a command-line command with the provided arguments.
    /// </summary>
    /// <param name="args">The command-line arguments array.</param>
    public async Task ExecuteCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        string command = args[0].ToLowerInvariant();
        var options = CliParser.Parse(args.Skip(1).ToArray());

        try
        {
            switch (command)
            {
                case "setup":
                    await _setupService.RunSetupAsync(options);
                    break;

                case "register":
                    await _setupService.RunRegisterAsync(options);
                    break;

                case "print-config":
                    _setupService.PrintConfig();
                    break;

                case "help":
                case "-h":
                case "--help":
                    PrintUsage();
                    break;

                default:
                    Console.WriteLine($"[ERROR] Unknown command: '{command}'. Run 'help' for usage.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Command '{command}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays usage information and available commands.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine($"""
Monitoring System Client Service

USAGE:
  <command> [options]

COMMANDS:
  setup          Initialize client configuration
  register       Register a new device
  print-config   Display current configuration
  help           Show this message

SETUP EXAMPLES:
  Regenerate an API key using credentials:
    setup --server-url=<url> --email=<email> --password=<password> --device-id=<id>

  Configure using an existing API key:
    setup --server-url=<url> --device-id=<id> --api-key=<key>

REGISTER EXAMPLE:
  Register this device to the server (automatically detects device info):
    register --server-url=<url> --email=<email> --password=<password> --device-name=<name>

SETUP OPTIONS:
  --interval=<seconds>                    Metrics collection interval (default: 10).
  --allow-self-signed-certificates=<true|false> Allow self-signed SSL certificates (default: true).

NOTES:
  * An existing account on the backend server is required (valid email & password).
  * The active configuration is saved locally in '{ConfigService.FileName}'.
""");
    }
}
