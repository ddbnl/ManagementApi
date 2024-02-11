using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace ManagementApi.Queue.Wrapper;

public static class QueueWrapper {

    private static ServiceBusClient? Client;

    public static void Connect() {
        if (Client is not null) {
            return;
        }

        string connectionString = Environment.GetEnvironmentVariable("queue")!;
        Client = new ServiceBusClient(connectionString, new DefaultAzureCredential());
        System.Console.WriteLine("Connected to Service bus namespace");
    }

    public static async Task SendMessage<T>(T message, string queue) {

        await using ServiceBusSender sender = Client!.CreateSender(queue);
        string json_message = JsonSerializer.Serialize(message);
        ServiceBusMessage bus_message = new(json_message);
        await sender.SendMessageAsync(bus_message);
    }

    public static async Task<(T?, ServiceBusReceivedMessage?)> ReceiveMessage<T>(string queue, TimeSpan timeout) {

        System.Console.WriteLine($"Get receiver {queue}");
        await using ServiceBusReceiver receiver = Client!.CreateReceiver(queue);
        System.Console.WriteLine($"Get msg {queue} {timeout}");
        ServiceBusReceivedMessage receivedMessage = await receiver.ReceiveMessageAsync(timeout);
        if (receivedMessage is null) {
            System.Console.WriteLine($"No msg");
            return (default(T), default(ServiceBusReceivedMessage));
        }
        System.Console.WriteLine($"Got msg {receivedMessage}");
        try {
            T? parsedMessage = JsonSerializer.Deserialize<T>(receivedMessage.Body);
            if (parsedMessage is null) {
                System.Console.WriteLine($"Corrupted message: {receivedMessage.Body}"); // TODO: Log
                return (default(T), default(ServiceBusReceivedMessage));
            }
            return (parsedMessage, receivedMessage);
        } catch {
            System.Console.WriteLine($"Corrupted message: {receivedMessage.Body}"); // TODO: Log
            return (default(T), default(ServiceBusReceivedMessage));
        }
    }
    
    public static async Task CloseMessage(string queue, ServiceBusReceivedMessage receivedMessage) {

        await using ServiceBusReceiver receiver = Client!.CreateReceiver(queue);
        await receiver.DeadLetterMessageAsync(receivedMessage);
    }
}
