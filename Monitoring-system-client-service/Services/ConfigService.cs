using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Monitoring_system_agent.Services
{
    internal static class ConfigService
    {
        public static bool TryLoadConfig<T>(string configPath, out T? model)
        {
            model = default;

            if (!File.Exists(configPath))
            {
                Console.WriteLine("File not found");
                return false;
            }

            string? content = TryReadFile(configPath);

            if (content is null)
            {
                Console.WriteLine("Error while loading file");
                return false;
            }

            model = JsonConvert.DeserializeObject<T>(content);

            if (model is null)
            {
                Console.WriteLine("Error while loading file");
                return false;
            }

            return true;
        }

        public static bool TryToSaveFile<T>(string configPath, T model)
        {
            string content = JsonConvert.SerializeObject(model, Formatting.Indented);

            try
            {
                File.WriteAllText(configPath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while saving file" + ex.Message);
                return false;
            }

            return true;
        }

        public static string? TryReadFile(string configPath)
        {
            string content;

            try
            {
                content = File.ReadAllText(configPath);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("File does not exist");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while loading file" + ex.Message);
                return null;
            }
            return content;
        }
    }
}
