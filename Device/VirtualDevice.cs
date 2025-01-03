using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System.Data;
using System.Net.Mime;
using System.Text;

namespace SDKDemo.Device
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;   //obiekt naszego klienta/urządzenia

        public VirtualDevice(DeviceClient deviceClient)
        {
            this.client = deviceClient;
        }


        //komunikacja: wysyłanie wiadomości
        #region Sedning Message D2C
        //region służy do dzielenia kodu, żeby ładnie oznacza różne "regiony"
        //poniższa metoda służy do wysyłania wiadomości z device do cloud
        //sposób wykorzystania, np.: device będzie wysyłać pewnie informacje (np. temperaturę)
        //a cloud będzie je jakoś przetwarzać u siebie
        //nrOfMessages to ile razy wiadomość ma być wysłana
        //delay to odstęp między tymi wiadomościami
        public async Task SendMessage(int nrOfMessages, int delay)
        {
            var rmd = new Random(); //losuje wartości do wysłania
            Console.WriteLine($"Device sending {nrOfMessages} message to IoTHub...\n"); //wiadomość do pokazania że no coś się dzieje tam
            for (int i = 0; i < nrOfMessages; i++)
            {
                var data = new
                {
                    temperature = rmd.Next(20, 35),  //losowanie temp
                    humidity = rmd.Next(60, 80), //losowanie wilgotności
                    msgCount = i    //która to z kolei wiadomość
                };
                //powyższy obiekt należy prekonwertować na json, aby można go było wysłać jako string(?)
                var dataString = JsonConvert.SerializeObject(data);
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString)); //tworzenie obiektu wiadomość, która wyśle tekst o takim kodowaniu i takim rozmiarze
                eventMessage.ContentType = MediaTypeNames.Application.Json; //określenie jakigo rodzaje wiadomość wysyła
                eventMessage.ContentEncoding = "utf-8";
                eventMessage.Properties.Add("tempAlert", (data.temperature > 30 ? "true" : "false"));
                //do wiadomości oprócz zmiennej przechowywującej treść dołączam zmienną tempAlert,
                //która jest true jeżeli temperatura przewyższa 30 stopni
                Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > Sending message: {i}, Data [{dataString}]");
                //ta wiadomość będzie pokazana w konsoli żeby pokazać progres

                await client.SendEventAsync(eventMessage);  //wysłanie wiadomości

                //ogarnięcie odstępu pomiędzy wiadomościami
                if (i < nrOfMessages - 1)
                {
                    await Task.Delay(delay);
                }
            }
            Console.WriteLine();    //to chyba jest \n
        }
        #endregion

        //komunikacja: odbieranie wiadomości
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


        //obsługiwanie metod urządzenia (można powiedzieć że to taka zdalna kontrola)
        #region Device Methods
        //obsługwanie metod wywołanych przez device IoTHub
        private async Task<MethodResponse> SendMessageHandler(MethodRequest methodRequest, object userContext)
        {
            //co za metoda została otrzymana
            Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name}");
            //co ta metoda w sobie zawiera (konwersja string json -> obiekt)
            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { nrOfMessages = default(int), delay = default(int) });

            //np. dana funkcja odpowiada na wywołanie metody -> wysłanie wiadomości
            await SendMessage(payload.nrOfMessages, payload.delay);
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


        //synchronizacja device twinów
        #region Device Twin
        public async Task UpdateTwinAsync()
        {
            var twin = await client.GetTwinAsync();
            Console.WriteLine($"\n Initial twin value received: \n{JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            Console.WriteLine();

            var reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastAppLaunch"] = DateTime.Now;
            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChange(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            Console.WriteLine($"\t Sending current time as reported property");
            TwinCollection reportedCollection = new TwinCollection();
            reportedCollection["DataTimeLastPropertyChangeReceived"] = DateTime.Now.ToLocalTime();

            await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
        }
        #endregion


        //handler dla obsługi eventów otrzymywanych od Cloud
        public async Task InitializeHandlers()
        {
            //jeżeli metoda od Microsoft set...async wykryje otrzymanie wiadomości to włączy metodę wyświetlającą wadomość
            await client.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, client);

            await client.SetMethodHandlerAsync("SendMessage", SendMessageHandler, client);
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);

            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
        }
    }
}
