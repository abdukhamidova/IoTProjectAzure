using SDKDemo.Device;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using Opc.UaFx;

string deviceConnectionString = "HostName=LittleHubIot.azure-devices.net;DeviceId=DeviceDemoSdk1;SharedAccessKey=Kh1uVOuO+CjHbvXCW3wPIbQnZuK3A1mXd1Dsp5SNuTk=";
//stworzenie klienta (klientem jest device w Azure)
using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
await deviceClient.OpenAsync(); //otwiera naszą instację: start komunikacji

//stworzenie wirtualnego urządzenia
var device = new VirtualDevice(deviceClient);
//await device.InitializeHandlers();  //inicjaliacja eventów - od teraz aplikacja będzie czekała na możliwe wiadomości i będzie mogła je obsłużyć
Console.WriteLine($"Connection success!");

using (var client = new OpcClient("opc.tcp://localhost:4840/"))
{
    client.Connect();
    OpcReadNode[] commands = new OpcReadNode[] {
    new OpcReadNode("ns=2;s=Device 1/ProductionStatus", OpcAttribute.DisplayName),  //0 telemetria    
        new OpcReadNode("ns=2;s=Device 1/ProductionStatus"),                        //1
    new OpcReadNode("ns=2;s=Device 1/ProductionRate", OpcAttribute.DisplayName),    //2 device twin
        new OpcReadNode("ns=2;s=Device 1/ProductionRate"),                          //3    
    new OpcReadNode("ns=2;s=Device 1/WorkorderId", OpcAttribute.DisplayName),       //4 telemetria
        new OpcReadNode("ns=2;s=Device 1/WorkorderId"),                             //5
    new OpcReadNode("ns=2;s=Device 1/Temperature", OpcAttribute.DisplayName),       //6 telemetria
        new OpcReadNode("ns=2;s=Device 1/Temperature"),                             //7
    new OpcReadNode("ns=2;s=Device 1/GoodCount", OpcAttribute.DisplayName),         //8 telemetria
    new OpcReadNode("ns=2;s=Device 1/GoodCount"),                                   //9
    new OpcReadNode("ns=2;s=Device 1/BadCount", OpcAttribute.DisplayName),          //10 telemetria
        new OpcReadNode("ns=2;s=Device 1/BadCount"),                                //11
    new OpcReadNode("ns=2;s=Device 1/DeviceError", OpcAttribute.DisplayName),       //12 error
    new OpcReadNode("ns=2;s=Device 1/DeviceError"),                                 //14    
};

    while (true)
    {
        //pobieranie w petli telemetrii z symulatora
        IEnumerable<OpcValue> job = client.ReadNodes(commands);
        device.SendMessage(job);
        // Wyświetlenie wyników
        foreach (var item in job)
        {
            Console.WriteLine(item.Value);
        }
        await Task.Delay(3000);
    }
}