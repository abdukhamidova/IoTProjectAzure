using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Data;
using System.Net.Mime;
using System.Text;

namespace SDKDemo.Device
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;   //obiekt naszego klienta/urządzenia
        private readonly string opcDeviceName;
        private readonly OpcClient opcClient;
        private int lastReportedStatus = 0;
        public VirtualDevice(DeviceClient deviceClient, string opcDeviceName, OpcClient opcClient)
        {    //konstruktor
            this.client = deviceClient;
            this.opcDeviceName = opcDeviceName;
            this.opcClient = opcClient;
        }

        [Flags]
        public enum DeviceStatus
        {
            None = 0,               // 0000
            EmergencyStop = 1,      // 0001
            PowerFailure = 2,       // 0010
            SensorFailure = 4,      // 0100
            Unknown = 8             // 1000
        }


        ///handler dla obsługi eventów otrzymywanych od Cloud
        public async Task InitializeHandlers()
        {//jeżeli metoda od Microsoft set...async wykryje otrzymanie wiadomości to włączy metodę wyświetlającą wadomość
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
            await client.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, client);
            await client.SetMethodHandlerAsync("EmergencyStop", DeviceStatusErrorHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", DeviceStatusErrorHandler, client);
            //await client.SetMethodHandlerAsync("SendMessage", SendMessageHandler, client);
            //await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            
            lastReportedStatus = await GetReportedDeviceStatusAsync();
        }

        // !!! do przemyslenia !!!
        public void JobManager(IEnumerable<OpcValue> job)
        {
            sendTelemetry(job);
            Task updateTwin = UpdateTwinAsync(job);
        }

        //komunikacja: wysyłanie wiadomości
        #region Set Message Data
        private async void sendTelemetry(IEnumerable<OpcValue> job)
        {
            //ustawianie telemetrii
            var data = new
            {
                ProductionStatus = job.ElementAt(1).Value,
                WorkerId = job.ElementAt(5).Value,
                Temperature = job.ElementAt(7).Value,
                GoodCount = job.ElementAt(9).Value,
                BadCount = job.ElementAt(11).Value
            };
            await SendMessage(data);
        }
        private async void sendDeviceStatus(int oldStatus, int newStatus)
        {
            //konwersja na flagi
            var oldStatusFlags = (DeviceStatus)oldStatus;
            var newStatusFlags = (DeviceStatus)newStatus;
            
            string oldStatusDescription = oldStatusFlags.ToString();
            string newStatusDescription = newStatusFlags.ToString();

            //tresc wiadomosci
            var data = new
            {
                Message = $"Status has changed from {oldStatusDescription} to {newStatusDescription}"
            };

            await SendMessage(data); // Załóżmy, że SendMessage obsługuje synchronizację
        }
        #endregion

        #region Sedning Message D2C
        //poniższa metoda służy do wysyłania wiadomości z device do cloud
        public async Task SendMessage(object data)
        {
            Console.WriteLine("\n Sending message... ");
            //formatowanie wiadomosci do wyslania
            var dataString = JsonConvert.SerializeObject(data);
            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";
            //wysłanie wiadomości
            await client.SendEventAsync(eventMessage);
            Console.WriteLine("\n FINISHED... ");
        }
        #endregion

        ///komunikacja: odbieranie wiadomości
        #region Receiving messege C2D

        private async Task OnC2dMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > C2D message callback - message received with id = {receivedMessage.MessageId}\n");
            PrintMessage(receivedMessage);  //wyświetlanie otrzymanej wiadomości
            await client.CompleteAsync(receivedMessage);    //informacja dla IoT Hub, że wiadomość została odczytana, więc może usuwać wiadomość z kolejki Device
            receivedMessage.Dispose();  //usuwa wiadomość
            //wiadomość jest czyszczona z kolejki, aby zrobić przejście dla pozostałych wiadomości
        }

        //funkcja wypisująca wiadomość
        private void PrintMessage(Message receivedMessage)
        {
            //odkodowujemy wiadomość, którą otrzymujemy (konwersja z utf8 na ASCII
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"\t \t Received message: {messageData} ");
            int propCount = 0;
            foreach (var prop in receivedMessage.Properties)
            {
                //wypisujemy jak dokładnie wygląda każda propercja
                Console.WriteLine($"\t\t Property {(propCount++)} > Key={prop.Key} : Value={prop.Value}");
            }
        }
        #endregion

        ///obsługiwanie metod urządzenia (można powiedzieć że to taka zdalna kontrola)
        #region Direct Methods
        //obsługwanie metod wywołanych przez device IoTHub
        private async Task<MethodResponse> DeviceStatusErrorHandler(MethodRequest methodRequest, object userContext)
        {
            //co za metoda została otrzymana
            var result = opcClient.CallMethod($"ns=2;s={opcDeviceName}", $"ns=2;s={opcDeviceName}/{methodRequest.Name}");
            if (result != null)
            {
                Console.WriteLine($"{methodRequest.Name} executed successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to execute {methodRequest.Name}.");
            }
            await Task.Delay(1000); //mowi ze brakuje await
            return new MethodResponse(0);
        }

        //w jaki sposób obsługiwać metody, dla których nie napisano oddzielnych funckji obsługiwania
        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name}");
            await Task.Delay(1000);
            return new MethodResponse(0);
        }
        #endregion


        ///synchronizacja device twinów
        #region Device Twin

        public async Task<int> GetReportedDeviceStatusAsync()
        {
            var twin = await client.GetTwinAsync();
            var reportedProperties = twin.Properties.Reported;

            if (reportedProperties.Contains("DeviceStatus"))
            {
                var deviceStatusArray = reportedProperties["DeviceStatus"] as JArray;
                var statusList = deviceStatusArray?.ToObject<List<string>>() ?? new List<string>();

                int deviceStatusNumber = 0;

                foreach (var status in statusList)
                {
                    if (Enum.TryParse<DeviceStatus>(status, true, out var parsedStatus))
                    {
                        deviceStatusNumber |= (int)parsedStatus; // Add the flag to the result
                    }
                }

                return deviceStatusNumber;
            }

            return 0;
        }

        public async Task UpdateTwinAsync(IEnumerable<OpcValue> job)
        {
            var twin = await client.GetTwinAsync();
            //Console.WriteLine($"\n Initial twin value received: \n{JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            //Console.WriteLine();

            var reportedProperties = new TwinCollection();
            //ustawienie Production Rate
            reportedProperties["ProductionRate"] = job.ElementAt(3).Value;

            #region Reported State
            //konwersja wartosci z job
            var deviceStatusValue = (int)job.ElementAt(13).Value;
            //jezeli status sie zmienil
            if (deviceStatusValue != lastReportedStatus) {
                sendDeviceStatus(lastReportedStatus, deviceStatusValue);
                lastReportedStatus = deviceStatusValue;
                var deviceStatus = (DeviceStatus)deviceStatusValue;
                
                //zrobienie listy z aktywnych flag
                var activeStatuses = Enum.GetValues(typeof(DeviceStatus))
                    .Cast<DeviceStatus>()
                    .Where(flag => flag != DeviceStatus.None && deviceStatus.HasFlag(flag))
                    .Select(flag => flag.ToString()) // Konwersja na string
                    .ToList();

                reportedProperties["DeviceStatus"] = new JArray(activeStatuses);
            }
            #endregion

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChange(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            Console.WriteLine($"\t Sending current time as reported property");
            TwinCollection reportedCollection = new TwinCollection();
            reportedCollection["ProductionRate"] = desiredProperties["ProductionRate"];
            using (var client = new OpcClient("opc.tcp://localhost:4840/"))
            {
                client.Connect();
                client.WriteNode($"ns=2;s={opcDeviceName}/ProductionRate", (int)desiredProperties["ProductionRate"]);
                Console.WriteLine("Updated");

            }
            await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
        }
        #endregion

    }
}
