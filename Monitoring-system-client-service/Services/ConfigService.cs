using Monitoring_system_agent.Models;
using Tomlyn;

namespace Monitoring_system_agent.Services;

public static class ConfigService
{
    public const string FileName = "agent_config.toml";

    public static bool SaveConfig(ConfigModel model)
    {
        try
        {
            string fileHeader = "# Monitoring Agent Configuration\n" +
                                $"# Generated at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
            string tomlContent = Toml.FromModel(model);
            File.WriteAllText(FileName, fileHeader + tomlContent);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to save configuration: {ex.Message}");
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
            Console.WriteLine($"[ERROR] Failed to read configuration file: {ex.Message}");
            return null;
        }
    }
}

