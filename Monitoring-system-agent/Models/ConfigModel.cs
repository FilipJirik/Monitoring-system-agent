using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring_system_agent.Models
{
    internal class ConfigModel
    {
        public string BaseUrl { get; set; } = default!;
        public string DeviceName { get; set; } = default!;
        public Guid DeviceId { get; set; }
        public string ApiKey { get; set; } = default!;
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
        public int Interval { get; set; } = 60;
    }
}
