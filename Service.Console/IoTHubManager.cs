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

        public async Task SendMessage(string messageText, string deviceId)
        {
            //tworzona zostaje zawartość wiadomości, obiekt jest konwertowany na jsona i wysłany do urządzenia
            var messageBody = new { text = messageText };
            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
            message.MessageId = Guid.NewGuid().ToString();
            await client.SendAsync(deviceId, message);
        }

        public async Task<int> ExecuteDeviceMethod(string methodName, string deviceId)
        {
            var method = new CloudToDeviceMethod(methodName);

            var methodBody = new { nrOfMessages = 5, delay = 500 };
            method.SetPayloadJson(JsonConvert.SerializeObject(methodBody));

            var result = await client.InvokeDeviceMethodAsync(deviceId, method);
            return result.Status;
        }


        //public async Task UpdateDesiredTwin(string deviceId, string propertyName, dynamic propertyValue)
        //{
        //    //propertyName - nazwa własciwości, którą rządamy aby Twin zgłaszał i jej wartość

        //    var twin = await registry.GetTwinAsync(deviceId);
        //    //modyfikujemy rządaną właściwość/dodajemy ją
        //    twin.Properties.Desired[propertyName] = propertyValue;
        //    await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
        //}

        public async Task ChangeProductionRateDesiredTwin(string connectionDeviceId)
        {
            var twin = await registry.GetTwinAsync(connectionDeviceId);
            //sprawdzenie zeby 
            var productionRate = twin.Properties.Reported["ProductionRate"];
            if (productionRate >= 10)
                twin.Properties.Desired["ProductionRate"] = productionRate - 10;
            
            await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
        }

    }
}
