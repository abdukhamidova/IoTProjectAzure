using SDKDemo.Device;  //biblioteka, którą uzywamy, namespace
using Microsoft.Azure.Devices.Client;

//connection string, ktory pozwala nam się połączyć aplikacji do IoT Hub
string deviceConnectionString = "HostName=LittleHubIot.azure-devices.net;DeviceId=DeviceDemoSdk1;SharedAccessKey=Kh1uVOuO+CjHbvXCW3wPIbQnZuK3A1mXd1Dsp5SNuTk=";
//stworzenie klienta (klientem jest device w Azure)
using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
await deviceClient.OpenAsync(); //otwiera naszą instację: start komunikacji

//stworzenie wirtualnego urządzenia
var device = new VirtualDevice(deviceClient); //pochodzi juz z naszej bibilioteki
await device.InitializeHandlers();  //inicjaliacja eventów - od teraz aplikacja będzie czekała na możliwe wiadomości i będzie mogła je obsłużyć
Console.WriteLine($"Connection success!");

await device.SendMessage(10, 1000);  //wysyłam 10 wiadomości z sekundowym odstępem
Console.WriteLine($"Finished!");
Console.ReadLine();