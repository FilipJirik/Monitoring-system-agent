using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service.CommandHandling;

public class CommandHandler
{
    private readonly SetupService _setupService;

    public CommandHandler(SetupService setupService)
    {
        _setupService = setupService;
    }

    public async Task ExecuteCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "setup":
                    await _setupService.RunSetupAsync(args);
                    break;
                case "register":
                    await _setupService.RunRegisterAsync(args);
                    break;
                case "print-config":
                    _setupService.PrintConfig();
                    break;
                case "help":
                case "--help":
                case "-h":
                    PrintUsage();
                    break;
                default:
                    Console.WriteLine($"[ERROR] Unknown command: {args[0]}, run help");
                    PrintUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Command failed: {ex.Message}");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
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

    GLOBAL OPTIONS:
      --interval=<seconds>   Metrics collection interval (default: 10). 
                             Can be appended to any 'setup' or 'register' command.

    NOTES:
      * An existing account on the backend server is required (valid email & password).
      * The active configuration is saved locally in 'agent_config.toml'.
    """);
    }
}
