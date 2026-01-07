using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring_system_agent.Models
{
    public class CreateDeviceModel
    {
        public string Name { get; set; } = default!;
        public string OperatingSystem { get; set; } = default!;
        public string IpAddress { get; set; } = default!;

        public string MacAddress { get; set; } = default!;
        public string Model { get; set; } = default!;
        public bool SshEnabled { get; set; } = false;
    }
}
