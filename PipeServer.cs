

using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;



public class PipeServer
{
    private record ClientInfo(NamedPipeServerStream Pipe, string Name);
    // This dictionary keeps track of connected clients, using a unique identifier (Guid) for each client and storing their associated pipe and name.
    private readonly ConcurrentDictionary<Guid, ClientInfo> _connectedClients = new();

    // This function starts the pipe server, which listens for incoming connections from modules and handles their messages.
    public void Start(string pipeName)
    {
        Console.WriteLine("Pipe server started.");

        while (true)
        {
            var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            pipe.WaitForConnection();

            // _ = creates a bacground task in a "fire and forget" way
            _ = Task.Run(() => HandleClient(pipe));
        }
    }


    // This function handles communication with a connected module, reading messages from it and responding with an acknowledgment.
    private async Task HandleClient(NamedPipeServerStream pipe)
    {
        var clientId = Guid.NewGuid();

        Console.WriteLine("Module connected.");

        var reader = new StreamReader(pipe);
        var clientName = await reader.ReadLineAsync() ?? "unknown";
        Console.WriteLine($"Module identified as: {clientName}");
        _connectedClients[clientId] = new ClientInfo(pipe, clientName);


        //await writer.WriteLineAsync("OK");

        // Listen for messages from the connected client until it disconnects.
        try
        {
            string? message;
            while ((message = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(message))
                    continue;

                //If enabled , it prints all messages in full, that passes through the core
                //Console.WriteLine($"[{clientName}] Received: {message}");
                await ParseMessage(clientId, clientName, message);
            }
        }
        catch (IOException)
        {
            Console.WriteLine("Could not write to client");
        }
        finally
        {
            _connectedClients.TryRemove(clientId, out _);
            Console.WriteLine($"Module '{clientName}' disconnected.");
            pipe.Dispose();
        }
    }

    // Parses a JSON message from a client and returns an acknowledgment or response.
    // Expected message format:
    // { "type": "message-type", "recipient": "recipient-name", "payload": "payload-json-data" }
    private async Task ParseMessage(Guid clientId, string clientName, string message)
    {
        try
        {
            var receivedMessage = JsonSerializer.Deserialize<TartaMessage>(message);

            if (receivedMessage.Type == "message")
            {
                // Handle the message type by sending it to the intended recipient if they are connected. LinQ (Look it up)
                var recipientClient = _connectedClients.Values.FirstOrDefault(c => c.Name == receivedMessage.Recipient);
                if (recipientClient != null)
                {

                    var writer = new StreamWriter(recipientClient.Pipe) { AutoFlush = true };
                    await writer.WriteLineAsync(message);
                    //If enabled, it will write of message when passing them on
                    //Console.WriteLine($"Message sent to '{receivedMessage.Recipient}': {message}");

                }
                else
                {
                    Console.WriteLine("Could not find receiving client in connected modules");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Could not parse message into object");

        }
    }

    public string[] ListConnectedClients()
    {
        return _connectedClients.Values.Select(c => c.Name).ToArray();
    }

    public void SendMessage(string clientName, string type, string subCategory, string sender, string recipient, string payload)
    {
        var recipientClient = _connectedClients.Values.FirstOrDefault(c => c.Name == clientName);
        if (recipientClient != null)
        {

            var writer = new StreamWriter(recipientClient.Pipe) { AutoFlush = true };
            var message = new TartaMessage(type, subCategory, sender, recipient, payload.Replace("\n", "").Replace("\r", ""));
            var json_formatted_message = System.Text.Json.JsonSerializer.Serialize(message);
            writer.WriteLine(json_formatted_message);



        }
    }
}