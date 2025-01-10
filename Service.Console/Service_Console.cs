using Microsoft.Azure.Devices;
using Service.Properties;
using ServiceSdkDemo.Console;
using ServiceSdkDemo.Lib;
using System.Runtime.Versioning;   //wpisywane wg namespace, ewentualnie to poprawic

string serviceConnectionString = Resources.iotHubConnectionString;

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
