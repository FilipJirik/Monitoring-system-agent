using System.Runtime.InteropServices;

namespace Monitoring_system_client_service.Validation
{
    public static class EnvironmentValidator
    {
        public static bool ValidatePlatform()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        public static void ThrowIfInvalidPlatform()
        {
            if (!ValidatePlatform())
            {
                throw new PlatformNotSupportedException("This application only runs on Linux.");
            }
        }
    }
}
