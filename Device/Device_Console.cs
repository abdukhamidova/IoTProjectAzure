using SDKDemo.Device;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using Opc.UaFx;
using System.Text.RegularExpressions;
using Microsoft.Azure.Amqp.Framing;
using Device.Properties;


public class Device_Console
{
    public static async Task Main(string[] args)
    {
        int deviceNum = 0;
        
        List<string> deviceConnectionStrings = new List<string>
        {
            Resources.iotDevice1ConnectionString,
            Resources.iotDevice2ConnectionString,
            Resources.iotDevice3ConnectionString
        };

        using var opcClient = new OpcClient(Resources.opcClientURL);
        opcClient.Connect();

        List<OpcNodeInfo> opcDevices = AvailableToConnect(opcClient, deviceConnectionStrings);
        List<VirtualDevice> virtualDevices = new List<VirtualDevice>();

        for(int i=0; i<opcDevices.Count; i++)
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStrings[i], TransportType.Mqtt);
            await deviceClient.OpenAsync();

            var device = new VirtualDevice(deviceClient, opcDevices[i].DisplayName, opcClient);
            await device.InitializeHandlers();

            virtualDevices.Add(device);

            Console.WriteLine($"\n Successfully connected {opcDevices[i].DisplayName} with a virtual device to IoT Hub.");
        }

        while (true)
        {
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

            /*
            Console.WriteLine($"\n Result of {opcDevices[deviceNum].DisplayName}...");
            foreach (var item in job)
                Console.WriteLine(item.Value);
            */

            deviceNum++;
            await Task.Delay(3000);
        }
    }

    private static List<OpcNodeInfo> AvailableToConnect(OpcClient opcClient, List<string> deviceConnectionStrings)
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

    private static List<OpcNodeInfo> GetDevices(OpcClient opcClient)
    {
        var nodes = opcClient.BrowseNode(OpcObjectTypes.ObjectsFolder);
        var devices = new List<OpcNodeInfo>();
        foreach (var child in nodes.Children())
        {
            if (IsDevice(child))
                devices.Add(child);
        }
        return devices;
    }
    private static bool IsDevice(OpcNodeInfo nodeInfo)
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