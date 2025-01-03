using Microsoft.Azure.Devices.Common.Exceptions;

namespace ServiceSdkDemo.Console
{
    internal static class FeatureSelector
    {
        public static void PrintMenu()
        {
            //w zależności od wartości zmiennej input w Program.cs będą wywołane dane metody
            System.Console.WriteLine(@"
    1 - C2D
    2 - Direct Method
    3 - Device Twin
    0 - Exit");
        }

        public static async Task Execute(int feature, Lib.IoTHubManager manager)
        {
            switch (feature)
            {
                case 1:
                    {
                        //jeżeli input był 1 -> wysyłana jest wiadomość
                        //wczytywana jest treść wiadomości
                        System.Console.WriteLine("\nType your message (confirm with enter):");
                        string messageText = System.Console.ReadLine() ?? string.Empty;

                        //wybiera urządzenie, do którego ma być wysłana wiadomość (adresat :))
                        //device id: azure -> iot hub -> devices -> nazwa w kolumnie Device ID (DeviceDemoSdk1) 
                        System.Console.WriteLine("Type your device ID (confirm with enter):");
                        string deviceId = System.Console.ReadLine() ?? string.Empty;

                        //wywołuje funkcje, która stworzy & wyśle wiadomość
                        await manager.SendMessage(messageText, deviceId);

                        System.Console.WriteLine("Message sent!");
                    }
                    break;
                case 2:
                    {
                        //
                        System.Console.WriteLine("\nType your device ID (confirm with enter):");
                        string deviceId = System.Console.ReadLine() ?? string.Empty;
                        try
                        {
                            var result = await manager.ExecuteDeviceMethod("SendMessages", deviceId);
                            System.Console.WriteLine($"Method executed with status {result}");
                        }
                        catch (DeviceNotFoundException)
                        {
                            System.Console.WriteLine("Device not connected!");
                        }
                    }
                    break;
                case 3:
                    {
                        //przekazujemy nazwę właściwości (pola)
                        System.Console.WriteLine("\nType property name (confirm with enter):");
                        string propertyName = System.Console.ReadLine() ?? string.Empty;

                        System.Console.WriteLine("\nType your device ID (confirm with enter):");
                        string deviceId = System.Console.ReadLine() ?? string.Empty;

                        //generujemy losową wartość
                        var random = new Random();
                        //aktualizujemy tą wartość na urządzeniu twin
                        await manager.UpdateDesiredTwin(deviceId, propertyName, random.Next());
                    }
                    break;
                default:
                    break;
            }
        }

        internal static int ReadInput()
        {
            var keyPressed = System.Console.ReadKey();
            var isParsed = int.TryParse(keyPressed.KeyChar.ToString(), out int result);
            return isParsed ? result : -1;
        }
    }
}
