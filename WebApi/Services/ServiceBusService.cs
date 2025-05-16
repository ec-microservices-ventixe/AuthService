using Azure.Messaging.ServiceBus;
using System.Diagnostics;
using System.Text.Json;

namespace WebApi.Services;

public class ServiceBusService(ServiceBusClient serviceBusClient)
{
    private readonly ServiceBusClient _serviceBusClient = serviceBusClient;

    public async Task<bool> AddToQueue(string queueName, object message)
    {
        try
        {
            string jsonMessage = JsonSerializer.Serialize(message);
            var sender = _serviceBusClient.CreateSender(queueName);
            var serviceBusMsg = new ServiceBusMessage(jsonMessage);
            await sender.SendMessageAsync(serviceBusMsg);
            await sender.DisposeAsync();

            return true;

        } catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return false;
        }
    }
}
