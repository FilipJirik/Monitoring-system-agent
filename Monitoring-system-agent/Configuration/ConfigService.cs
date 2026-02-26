using Monitoring_system_client_service.Models;
using Tomlyn;

namespace Monitoring_system_client_service.Configuration;

/// <summary>
/// Manages the loading and saving of the monitoring agent configuration.
/// Handles serialization/deserialization of configuration to/from TOML files.
/// </summary>
public static class ConfigService
{
    public const string FileName = "agent_config.toml";

    private const string FileHeaderFormat = "# Monitoring Agent Configuration\n# Generated at {0:yyyy-MM-dd HH:mm:ss}\n\n";

    /// <summary>
    /// Checks whether the configuration file exists and validates it can be accessed
    /// </summary>
    /// <returns>
    /// True if the configuration file exists and is accessible; false otherwise.
    /// </returns>
    public static bool ValidateConfigFileExists()
    {
        if (File.Exists(FileName))
            return true;

        Console.Error.WriteLine($"[ERROR] Configuration file '{FileName}' not found");
        Console.Error.WriteLine("[ERROR] Run 'setup' or 'register' command first");
        return false;
    }

    /// <summary>
    /// Saves the configuration model to the TOML configuration file.
    /// Includes a timestamp header indicating when the configuration was generated.
    /// </summary>
    /// <param name="model">The configuration model to save.</param>
    /// <returns>
    /// True if the configuration was saved successfully; false if an error occurred.
    /// Errors are written to stderr but not thrown (fail-safe behavior).
    /// </returns>
    public static bool SaveConfig(ConfigModel model)
    {
        try
        {
            string header = string.Format(FileHeaderFormat, DateTime.Now);
            string tomlContent = Toml.FromModel(model);
            File.WriteAllText(FileName, header + tomlContent);
            return true;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[ERROR] I/O error while saving configuration: {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"[ERROR] Permission denied while saving configuration: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to save configuration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reads and returns the raw contents of the configuration file.
    /// </summary>
    /// <returns>
    /// The configuration file contents if successful; null if the file does not exist or cannot be read.
    /// </returns>
    public static string? ReadConfig()
    {
        if (!File.Exists(FileName))
            return null;

        try
        {
            return File.ReadAllText(FileName);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[ERROR] I/O error while reading configuration: {ex.Message}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"[ERROR] Permission denied while reading configuration: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to read configuration file: {ex.Message}");
            return null;
        }
    }
}
