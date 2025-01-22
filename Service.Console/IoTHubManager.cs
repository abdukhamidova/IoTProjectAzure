using Microsoft.Azure.Devices;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Text;

namespace ServiceSdkDemo.Lib
{
    public class IoTHubManager
    {
        private readonly ServiceClient client;
        private readonly RegistryManager registry;

        public IoTHubManager(ServiceClient client, RegistryManager registry)
        {
            this.client = client;
            this.registry = registry;
        }

        public async Task ChangeProductionRateDesiredTwin(string connectionDeviceId)
        {
            var twin = await registry.GetTwinAsync(connectionDeviceId);
            var productionRate = twin.Properties.Reported["ProductionRate"];
            if (productionRate >= 10)
                twin.Properties.Desired["ProductionRate"] = productionRate - 10;
            
            await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
        }

        public async Task SetEmergencyErrorTriggerDesiredTwin(string connectionDeviceId, int occurredErrors)
        {
            var twin = await registry.GetTwinAsync(connectionDeviceId);
            if (occurredErrors > 3)
                twin.Properties.Desired["EmergencyTrigger"] = 1;
            else twin.Properties.Desired["EmergencyTrigger"] = 0;

            await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
        }
    }
}
