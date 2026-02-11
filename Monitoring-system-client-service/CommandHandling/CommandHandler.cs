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
                case "login":
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
                    Console.WriteLine($"[ERROR] Unknown command: {args[0]}");
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
        Console.WriteLine("Monitoring System Client Service");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  setup              Initialize client configuration");
        Console.WriteLine("  register           Register a new device");
        Console.WriteLine("  print-config       Display current configuration");
        Console.WriteLine("  help               Show this message");
        Console.WriteLine();
        Console.WriteLine("Setup Examples:");
        Console.WriteLine("  Regenerate API key using credentials:");
        Console.WriteLine("    setup --server-url=<url> --email=<email> --password=<password> --device-id=<id>");
        Console.WriteLine();
        Console.WriteLine("  Configure using existing API key:");
        Console.WriteLine("    setup --server-url=<url> --device-id=<id> --api-key=<key>");
        Console.WriteLine();
        Console.WriteLine("Register Example:");
        Console.WriteLine("  register --server-url=<url> --email=<email> --password=<password> --device-name=<name>");
        Console.WriteLine();
        Console.WriteLine("Optional parameter for setup/register:");
        Console.WriteLine("  --interval=<seconds>  Metrics collection interval (default: 10)");
    }
}
