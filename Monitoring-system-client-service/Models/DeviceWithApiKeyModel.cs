using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring_system_agent.Models
{
    public class DeviceWithApiKeyModel
    {
        public string Id { get; set; } = default!;
        public string ApiKey { get; set; } = default!;
        public string SetupCommand { get; set; } = default!;
    }
}
