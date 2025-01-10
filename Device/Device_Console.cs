using SDKDemo.Device;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using Opc.UaFx;
using System.Text.RegularExpressions;


public class Device_Console
{
    
    public static async Task Main(string[] args)
    {
        int deviceNum = 0;
        //rzeba to przerzucic do pliku konfiguracyjnego
        List<string> deviceConnectionStrings = new List<string>
        {
            "HostName=LittleHubIot.azure-devices.net;DeviceId=DeviceDemoSdk1;SharedAccessKey=Kh1uVOuO+CjHbvXCW3wPIbQnZuK3A1mXd1Dsp5SNuTk=",
            "HostName=LittleHubIot.azure-devices.net;DeviceId=DeviceDemoSdk2;SharedAccessKey=2mkX8njZsSzJoasb5OcXuvNBpzYSkeA9h5Kw3wlfxos=",
            "HostName=LittleHubIot.azure-devices.net;DeviceId=DeviceDemoSdk3;SharedAccessKey=/M998T45e+8E9Y7sfLTYbkuISevqWEvrJ74rNPPqDJA="
        };

        //polaczenie z serwerem OPC
        using var opcClient = new OpcClient("opc.tcp://localhost:4840/");
        opcClient.Connect();

        //lista urzadzen z OPC
        List<OpcNodeInfo> opcDevices = AvailableToConnect(opcClient, deviceConnectionStrings);
        //lista wirtualnych urzadzen
        List<VirtualDevice> virtualDevices = new List<VirtualDevice>();

        //tworzenie urzadzen wirtualnych
        for(int i=0; i<opcDevices.Count; i++)
        {
            //tworzenie polaczenia
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStrings[i], TransportType.Mqtt);
            await deviceClient.OpenAsync(); //otwiera naszą instację: start komunikacji

            //tworzenie virtual device
            var device = new VirtualDevice(deviceClient, opcDevices[i].DisplayName, opcClient);
            await device.InitializeHandlers(); //inicjalizacja handlerow

            // Dodanie urządzenia do listy
            virtualDevices.Add(device);

            Console.WriteLine($"\n Successfully connected {opcDevices[i].DisplayName} with a virtual device to IoT Hub.");
        }

        while (true)
        {
            //aktualnie przegladane urzadzenie
            if (deviceNum == opcDevices.Count)
            {
                deviceNum = 0;
            }
            string deviceName = opcDevices[deviceNum].DisplayName;

            OpcReadNode[] commands = new OpcReadNode[] {
            new OpcReadNode($"ns=2;s={deviceName}/ProductionStatus", OpcAttribute.DisplayName),  //0 telemetria    
                new OpcReadNode($"ns=2;s={deviceName}/ProductionStatus"),                        //1
            new OpcReadNode($"ns=2;s={deviceName}/ProductionRate", OpcAttribute.DisplayName),    //2 device twin
                new OpcReadNode($"ns=2;s={deviceName}/ProductionRate"),                          //3    
            new OpcReadNode($"ns=2;s={deviceName}/WorkorderId", OpcAttribute.DisplayName),       //4 telemetria
                new OpcReadNode($"ns=2;s={deviceName}/WorkorderId"),                             //5
            new OpcReadNode($"ns=2;s={deviceName}/Temperature", OpcAttribute.DisplayName),       //6 telemetria
                new OpcReadNode($"ns=2;s={deviceName}/Temperature"),                             //7
            new OpcReadNode($"ns=2;s={deviceName}/GoodCount", OpcAttribute.DisplayName),         //8 telemetria
            new OpcReadNode($"ns=2;s={deviceName}/GoodCount"),                                   //9
            new OpcReadNode($"ns=2;s={deviceName}/BadCount", OpcAttribute.DisplayName),          //10 telemetria
                new OpcReadNode($"ns=2;s={deviceName}/BadCount"),                                //11
            new OpcReadNode($"ns=2;s={deviceName}/DeviceError", OpcAttribute.DisplayName),       //12 error
            new OpcReadNode($"ns=2;s={deviceName}/DeviceError"),                                 //13    
            };

            IEnumerable<OpcValue> job = opcClient.ReadNodes(commands);
            virtualDevices[deviceNum].JobManager(job);

            Console.WriteLine($"\n Result of {opcDevices[deviceNum].DisplayName}...");
            // Wyświetlenie wyników
            foreach (var item in job)
            {
                Console.WriteLine(item.Value);
            }

            deviceNum++;
            await Task.Delay(3000);
        }



        /* device.JobManager(job);
        //stworzenie klienta (klientem jest device w Azure)
        using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
        await deviceClient.OpenAsync(); //otwiera naszą instację: start komunikacji

        //stworzenie wirtualnego urządzenia
        var device = new VirtualDevice(deviceClient);
        await device.InitializeHandlers();  //inicjaliacja eventów - od teraz aplikacja będzie czekała na możliwe wiadomości i będzie mogła je obsłużyć

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
            new OpcReadNode("ns=2;s=Device 1/DeviceError"),                                 //13    
        };

            while (true)
            {
                //pobieranie w petli telemetrii z symulatora
                IEnumerable<OpcValue> job = client.ReadNodes(commands);
                device.JobManager(job);
                // Wyświetlenie wyników
                foreach (var item in job)
                {
                    Console.WriteLine(item.Value);
                }
                await Task.Delay(3000);
            }
        }*/
    }

    private static List<OpcNodeInfo> AvailableToConnect(OpcClient opcClient, List<string> deviceConnectionStrings)  //laczy urzadzenia z OPC z tymi w Azure
    {
        List<OpcNodeInfo> devices = new List<OpcNodeInfo> ();
        devices = GetDevices(opcClient);
        if (devices.Count == 0)
        {
            throw new ArgumentException("Devices not found.");
        }
        else if (devices.Count <= deviceConnectionStrings.Count)
        {
            Console.WriteLine("Accessing connection strings...");
            return devices;
        }
        else
        {
            throw new ArgumentException("Not enough connection strings.\n" +
                                        $"Please, add {devices.Count - deviceConnectionStrings.Count} connection string(s).");
        }
    }

    private static List<OpcNodeInfo> GetDevices(OpcClient opcClient) //pobiera urzadzenia z OPC
    {
        var nodes = opcClient.BrowseNode(OpcObjectTypes.ObjectsFolder); //pobranie wszystkich wezlow z OPC
        var devices = new List<OpcNodeInfo>();
        foreach (var child in nodes.Children()) //poszukiwanie urzedzen w wezlach
        {
            if (IsDevice(child))
                devices.Add(child);
        }
        return devices;
    }
    private static bool IsDevice(OpcNodeInfo nodeInfo) //filtruje urzadzenia w OPC
    {
        string pattern = @"^Device [0-9]+$";
        Regex correctName = new Regex(pattern);
        string nodeName = nodeInfo.Attribute(OpcAttribute.DisplayName).Value.ToString();
        Match matchedName = correctName.Match(nodeName);
        if (matchedName.Success)
            return true;
        else
            return false;
    }
}





