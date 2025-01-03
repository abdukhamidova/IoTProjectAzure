using Microsoft.Azure.Devices;
using ServiceSdkDemo.Console;
using ServiceSdkDemo.Lib;   //wpisywane wg namespace, ewentualnie to poprawic

string serviceConnectionString = "HostName=LittleHubIot.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=O7eYa+R8qN91t7kaijSqLLTTEISMjxV4iAIoTDeoUU0=";

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);

//input - przyjmuje wartość z konsoli
int input;
do
{
    FeatureSelector.PrintMenu();
    input = FeatureSelector.ReadInput();
    await FeatureSelector.Execute(input, manager);  //przekaże wczytany input do switcha
} while (input != 0);
