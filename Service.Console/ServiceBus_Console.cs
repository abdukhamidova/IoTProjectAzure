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
string queueName = Resources.productionKPIQueueName;

//połączenie z Busem
await using ServiceBusClient client = new ServiceBusClient(sbConnectionString);
await using ServiceBusProcessor processorKPI = client.CreateProcessor(queueName);  //przetwarza wiadomosci kpi

//jezeli taka wiadomosc jest przeslana to przetwarzamy ta wiadomosc w ten sposob
processorKPI.ProcessMessageAsync += Processor_ProcessMessageAsync;
processorKPI.ProcessErrorAsync += Process_ErrorAsync;


await processorKPI.StartProcessingAsync();

Console.WriteLine("Waiting for messages... Press ENTER to stop.");
Console.ReadLine();

Console.WriteLine("\n Stopping receiving messages.");
await processorKPI.StopProcessingAsync();
Console.WriteLine("\n Stopped receiving messages.");


async Task Processor_ProcessMessageAsync(ProcessMessageEventArgs arg)
{
    //pobranie wiadomosci z kolejki
    Console.WriteLine($"RECEIVED MESSAGE: \n\t {arg.Message.Body}");    //pobieramy taka wiadomosc

    // wyciagniecie potrzebnych wartosci
    // Deserializacja wiadomości JSON do JObject
    JObject productionRate = JObject.Parse(arg.Message.Body.ToString());

    // Wyciąganie wartości z JObject
    string connectionDeviceId = productionRate["ConnectionDeviceId"].ToString();
    double kpi = productionRate["KPI"].Value<double>();
    //Console.WriteLine($"Connection Device ID: {connectionDeviceId}");
    //Console.WriteLine($"KPI: {kpi}");

    //przetwarzanie KPI
    if (kpi < 90)
    {
        manager.ChangeProductionRateDesiredTwin(connectionDeviceId);
    }
    await arg.CompleteMessageAsync(arg.Message);
    //wiadomosc uznawane jest za skonczona/przetworzona wiec usuwamy ja z kolejki
    //jezeli sie cos wczesniej wywali (przed tym Complete...) to wiadomosc trafia do dead letteringu
}

Task Process_ErrorAsync(ProcessErrorEventArgs arg)
{
    Console.WriteLine(arg.Exception.ToString());
    return Task.CompletedTask;
}
#endregion