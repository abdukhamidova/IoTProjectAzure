using Azure.Messaging.ServiceBus;
using Service.Properties;
using Microsoft.Azure.Devices;
using ServiceSdkDemo.Lib;
using System.Text.Json;
using Newtonsoft.Json.Linq;

#region Service Connection
string serviceConnectionString = Resources.iotHubConnectionString;

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);
#endregion

#region Service Bus
string sbConnectionString = Resources.serviceBusConnectionString;


await using ServiceBusClient client = new ServiceBusClient(sbConnectionString);
await using ServiceBusProcessor processorKPI = client.CreateProcessor(Resources.productionKPIQueueName);
await using ServiceBusProcessor processorErrors = client.CreateProcessor(Resources.deviceErrorQueueName);

processorKPI.ProcessMessageAsync += Processor_ProcessKPIMessageAsync;
processorKPI.ProcessErrorAsync += Process_ErrorAsync;

processorErrors.ProcessMessageAsync += Processor_ProcessErrorMessageAsync;
processorErrors.ProcessErrorAsync += Process_ErrorAsync;


await processorKPI.StartProcessingAsync();
await processorErrors.StartProcessingAsync();

Console.WriteLine("Waiting for messages... Press ENTER to stop.");
Console.ReadLine();

Console.WriteLine("\n Stopping receiving messages.");
await processorKPI.StopProcessingAsync();
await processorErrors.StopProcessingAsync();
Console.WriteLine("\n Stopped receiving messages.");


async Task Processor_ProcessKPIMessageAsync(ProcessMessageEventArgs arg)
{
    Console.WriteLine($"RECEIVED MESSAGE: \n\t {arg.Message.Body}");

    JObject productionRate = JObject.Parse(arg.Message.Body.ToString());

    string connectionDeviceId = productionRate["ConnectionDeviceId"].ToString();
    double kpi = productionRate["KPI"].Value<double>();

    if (kpi < 90)
    {
        manager.ChangeProductionRateDesiredTwin(connectionDeviceId);
    }
    await arg.CompleteMessageAsync(arg.Message);
}

async Task Processor_ProcessErrorMessageAsync(ProcessMessageEventArgs arg)
{
    Console.WriteLine($"RECEIVED MESSAGE: \n\t {arg.Message.Body}");

    JObject deviceErrors = JObject.Parse(arg.Message.Body.ToString());

    string connectionDeviceId = deviceErrors["ConnectionDeviceId"].ToString();
    int occurredErrors = (int)deviceErrors["sumErrors"];
    manager.SetEmergencyErrorTriggerDesiredTwin(connectionDeviceId, occurredErrors);

    await arg.CompleteMessageAsync(arg.Message);
}

Task Process_ErrorAsync(ProcessErrorEventArgs arg)
{
    Console.WriteLine(arg.Exception.ToString());
    return Task.CompletedTask;
}
#endregion