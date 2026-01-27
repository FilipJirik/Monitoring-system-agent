using Monitoring_system_agent.Models;
using Tomlyn;

namespace Monitoring_system_agent.Services;

/// <summary>
/// Service for managing configuration file operations.
/// </summary>
public static class ConfigService
{
    public const string FileName = "agent_config.toml";

    /// <summary>
    /// Attempts to load the configuration from the TOML file.
    /// </summary>
    /// <returns>A ConfigModel if the file exists and can be parsed or null</returns>
    public static ConfigModel? TryLoadConfig()
    {
        string? content = TryReadFile();

        if (content is null)
            return null;

        return Toml.ToModel<ConfigModel>(content);
    }

    /// <summary>
    /// Saves the configuration model to a TOML file.
    /// </summary>
    /// <param name="model">The configuration model to save</param>
    /// <returns>True if the file was saved successfully, false if not</returns>
    public static bool TrySaveFile(ConfigModel model)
    {
        try
        {
            string fileHeader = "# Monitoring Agent Configuration\n" +
                                $"# Generated at {DateTime.Now}\n\n";

            string tomlContent = Toml.FromModel(model);

            File.WriteAllText(FileName, fileHeader + tomlContent);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to read the configuration file from disk.
    /// </summary>
    /// <returns>The file contents as a string if successful, null otherwise</returns>
    public static string? TryReadFile()
    {
        if (!File.Exists(FileName))
            return null;
            
        string? content;

        try
        {
            content = File.ReadAllText(FileName);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("Configuration file does not exist");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error while loading configuration file" + ex.Message);
            return null;
        }
        return content;
    }
}

