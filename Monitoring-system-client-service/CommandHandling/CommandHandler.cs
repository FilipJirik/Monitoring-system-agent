using Monitoring_system_client_service.Services;

namespace Monitoring_system_client_service.CommandHandling
{
    public class CommandHandler
    {
        private readonly SetupService _setupService;

        public CommandHandler(SetupService setupService)
        {
            _setupService = setupService;
        }

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "setup":
                case "login":
                    await _setupService.RunSetupAsync(args);
                    break;
                case "register":
                    await _setupService.RunCreateDeviceAsync(args);
                    break;
                case "print-config":
                    _setupService.PrintConfig();
                    break;
                case "help":
                    PrintUsage();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    break;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Monitoring System Client Service");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  setup              Initialize and configure the client");
            Console.WriteLine("  login              Initialize and configure the client (alias for setup)");
            Console.WriteLine("  register           Register a new device");
            Console.WriteLine("  print-config       Display the current configuration");
            Console.WriteLine("  help               Show this help message");
            Console.WriteLine();
            Console.WriteLine("Setup/Login Examples:");
            Console.WriteLine("  Regenerate API key using email and password:");
            Console.WriteLine("    setup --server-url=<url> --email=<email> --password=<password> --device-id=<deviceId>");
            Console.WriteLine();
            Console.WriteLine("  Configure using existing device ID and API key:");
            Console.WriteLine("    setup --server-url=<url> --device-id=<deviceId> --api-key=<apiKey>");
            Console.WriteLine();
            Console.WriteLine("Registration Example:");
            Console.WriteLine("  register --server-url=<url> --email=<email> --password=<password> --device-name=<deviceName>");
            Console.WriteLine();
            Console.WriteLine("Optional parameter:");
            Console.WriteLine("  --interval=<seconds>  Interval for metrics collection (default: 10)");
            Console.WriteLine();
            Console.WriteLine("Run without arguments to start the monitoring service.");
        }
    }
}
