using Azure;
using Azure.Communication.Email;
using Device.Properties;
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
        private readonly DeviceClient client;
        private readonly string opcDeviceName;
        private readonly OpcClient opcClient;
        private int lastReportedStatus = 0;

        private readonly string emailConnectionString = Resources.iotEmailConnectionString;
        private readonly EmailClient senderClient;
        private readonly string senderAddress = Resources.senderEmailAddress;
        private readonly string receiverAddress = Resources.receiverEmailAddress;
        public VirtualDevice(DeviceClient deviceClient, string opcDeviceName, OpcClient opcClient)
        {
            this.client = deviceClient;
            this.opcDeviceName = opcDeviceName;
            this.opcClient = opcClient;

            this.senderClient = new EmailClient(emailConnectionString);
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

        public async Task InitializeHandlers()
        {
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
            await client.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, client);

            await client.SetMethodHandlerAsync("EmergencyStop", DeviceStatusErrorHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", DeviceStatusErrorHandler, client);
           
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            
            lastReportedStatus = await GetReportedDeviceStatusAsync();
        }

        public void JobManager(IEnumerable<OpcValue> job)
        {
            sendTelemetry(job);
            Task updateTwin = UpdateTwinAsync(job);
        }

        #region Set Message Data
        private async void sendTelemetry(IEnumerable<OpcValue> job)
        {
            Console.WriteLine($"\nSending telemetry to {opcDeviceName}... ");
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
            Console.WriteLine($"\nSending device status to {opcDeviceName}... ");
            var oldStatusFlags = (DeviceStatus)oldStatus;
            var newStatusFlags = (DeviceStatus)newStatus;
            
            string oldStatusDescription = oldStatusFlags.ToString();
            string newStatusDescription = newStatusFlags.ToString();

            var data = new
            {
                Message = $"Status has changed from {oldStatusDescription} to {newStatusDescription}"
            };

            await SendMessage(data);
        }

        private async void sendNewDeviceError(int oldStatus, int newStatus)
        {
            Console.WriteLine($"\nSending new {opcDeviceName} error... ");
            int addedErrors = newStatus & ~oldStatus;

            var errorNames = Enum.GetValues(typeof(DeviceStatus))
                .Cast<DeviceStatus>()
                .Where(status => (addedErrors & (int)status) != 0)
                .Select(status => status.ToString())
                .ToArray();

            if (errorNames.Length > 0)
            {
                string errorString = string.Join(", ", errorNames);
                await SendEmailMessageAsync(errorString);
            }

            var data = new { NewError = errorNames.Length };
            await SendMessage(data);
        }
        #endregion

        #region Sedning Message D2C
        public async Task SendMessage(object data)
        {
            var dataString = JsonConvert.SerializeObject(data);
            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";
            
            await client.SendEventAsync(eventMessage);
            Console.WriteLine("\n ...FINISHED");
        }
        #endregion

        #region Receiving messege C2D

        private async Task OnC2dMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > C2D message callback - message received with id = {receivedMessage.MessageId}\n");
            PrintMessage(receivedMessage);
            await client.CompleteAsync(receivedMessage);
            receivedMessage.Dispose();
        }

        private void PrintMessage(Message receivedMessage)
        {
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"\t \t Received message: {messageData} ");
            int propCount = 0;
            foreach (var prop in receivedMessage.Properties)
            {
                Console.WriteLine($"\t\t Property {(propCount++)} > Key={prop.Key} : Value={prop.Value}");
            }
        }
        #endregion

        #region Sending Email
        private async Task SendEmailMessageAsync(string errorString)
        {
            try
            {
                var subject = $"New error on {opcDeviceName}";
                var body = $"The following errors have been detected: {errorString}.";

                EmailContent emailContent = new EmailContent(subject);
                emailContent.PlainText = body;
                EmailMessage emailMessage = new EmailMessage(senderAddress, receiverAddress, emailContent);

                EmailSendOperation emailSendOperation = await senderClient.SendAsync(Azure.WaitUntil.Completed, emailMessage);
                Console.WriteLine($"\nFinished sending email about an error: {errorString}");
            }
            catch (RequestFailedException ex)
            {
                throw new RequestFailedException(ex.ErrorCode);
            }
        }
        #endregion

        #region Direct Methods
        private async Task<MethodResponse> DeviceStatusErrorHandler(MethodRequest methodRequest, object userContext)
        {
            var result = opcClient.CallMethod($"ns=2;s={opcDeviceName}", $"ns=2;s={opcDeviceName}/{methodRequest.Name}");
            if (result != null)
            {
                Console.WriteLine($"{methodRequest.Name} executed successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to execute {methodRequest.Name}.");
            }
            await Task.Delay(1000);
            return new MethodResponse(0);
        }
        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\t TRIGGERED METHOD: {methodRequest.Name}");
            await Task.Delay(1000);
            return new MethodResponse(0);
        }
        #endregion

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
                        deviceStatusNumber |= (int)parsedStatus;
                    }
                }

                return deviceStatusNumber;
            }

            return 0;
        }

        public async Task UpdateTwinAsync(IEnumerable<OpcValue> job)
        {
            var twin = await client.GetTwinAsync();

            var reportedProperties = new TwinCollection();
            reportedProperties["ProductionRate"] = job.ElementAt(3).Value;

            #region Reported State
            var deviceStatusValue = (int)job.ElementAt(13).Value;
            if (deviceStatusValue != lastReportedStatus) {
                sendNewDeviceError(lastReportedStatus, deviceStatusValue);
                sendDeviceStatus(lastReportedStatus, deviceStatusValue);

                lastReportedStatus = deviceStatusValue;
                var deviceStatus = (DeviceStatus)deviceStatusValue;
                
                var activeStatuses = Enum.GetValues(typeof(DeviceStatus))
                    .Cast<DeviceStatus>()
                    .Where(flag => flag != DeviceStatus.None && deviceStatus.HasFlag(flag))
                    .Select(flag => flag.ToString())
                    .ToList();

                reportedProperties["DeviceStatus"] = new JArray(activeStatuses);
            }
            #endregion

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChange(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            
            TwinCollection reportedCollection = new TwinCollection();

            if (desiredProperties["EmergencyTrigger"] == 1)
            {
                var result = opcClient.CallMethod($"ns=2;s={opcDeviceName}", $"ns=2;s={opcDeviceName}/EmergencyStop");
                if (result != null)
                {
                    Console.WriteLine($"/EmergencyStop executed successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to execute /EmergencyStop.");
                }
            }

            reportedCollection["ProductionRate"] = desiredProperties["ProductionRate"];
            opcClient.WriteNode($"ns=2;s={opcDeviceName}/ProductionRate", (int)desiredProperties["ProductionRate"]);

            await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
        }
        #endregion

    }
}
