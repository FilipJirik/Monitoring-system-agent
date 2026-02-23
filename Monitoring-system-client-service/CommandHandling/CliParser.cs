namespace Monitoring_system_client_service.CommandHandling;

/// <summary>
/// Parses CLI arguments in the format --key=value into a dictionary.
/// </summary>
public static class CliParser
{
    private const string ArgPrefix = "--";
    private const char KeyValueSeparator = '=';

    /// <summary>
    /// Parses command-line arguments (excluding the command name itself).
    /// Only arguments starting with "--" are recognized; others are ignored.
    /// </summary>
    public static Dictionary<string, string?> Parse(string[] args)
    {
        var result = new Dictionary<string, string?>();

        foreach (var arg in args)
        {
            if (!arg.StartsWith(ArgPrefix))
                continue;

            var keyValue = arg.Substring(ArgPrefix.Length)
                          .Split(KeyValueSeparator, 2);

            result[keyValue[0]] = keyValue.Length == 2 ? keyValue[1] : null;
        }

        return result;
    }
}
