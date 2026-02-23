using Monitoring_system_client_service.Models;
using Tomlyn;

namespace Monitoring_system_client_service.Configuration;

public static class ConfigService
{
    public const string FileName = "agent_config.toml";

    private const string FileHeaderFormat = "# Monitoring Agent Configuration\n# Generated at {0:yyyy-MM-dd HH:mm:ss}\n\n";

    /// <summary>
    /// Checks whether the configuration file exists.
    /// Writes error messages if not found.
    /// </summary>
    public static bool ValidateConfigFileExists()
    {
        if (File.Exists(FileName))
            return true;

        Console.Error.WriteLine($"[ERROR] Configuration file '{FileName}' not found");
        Console.Error.WriteLine("[ERROR] Run 'setup' or 'register' command first");
        return false;
    }

    public static bool SaveConfig(ConfigModel model)
    {
        try
        {
            string header = string.Format(FileHeaderFormat, DateTime.Now);
            string tomlContent = Toml.FromModel(model);
            File.WriteAllText(FileName, header + tomlContent);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to save configuration: {ex.Message}");
            return false;
        }
    }

    public static string? ReadConfig()
    {
        if (!File.Exists(FileName))
            return null;

        try
        {
            return File.ReadAllText(FileName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to read configuration file: {ex.Message}");
            return null;
        }
    }
}
