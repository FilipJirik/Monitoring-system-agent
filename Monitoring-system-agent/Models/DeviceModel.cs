using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring_system_agent.Models
{
    internal class DeviceModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string OperatingSystem { get; set; } = default!;
        public string IpAddress { get; set; } = default!;
        public string MacAddress { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Model { get; set; } = default!;
        public bool SshEnabled { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Guid OwnerId { get; set; }
        public string OwnerUsername { get; set; } = default!;

        public Guid PictureId { get; set; }

        public string ApiKey { get; set; } = default!;
    }
}
