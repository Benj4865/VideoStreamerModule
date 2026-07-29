using System.IO.Pipes;
using Microsoft.VisualBasic;

public class TartaMessage(string type, string subCategory, string sender, string recipient, string payload) : EventArgs
{
    public string Type { get; } = type;
    public string SubCategory { get; } = subCategory;
    public string Sender { get; } = sender;
    public string Recipient { get; } = recipient;
    public string Payload { get; } = payload;
}

public class PipeMessageEventArgs(string pipeName, TartaMessage message) : EventArgs
{
    public string PipeName { get; } = pipeName;
    public TartaMessage TartaMessage { get; } = message;
}

public class Connection(NamedPipeClientStream pipe, StreamReader reader, StreamWriter writer)
{
    public NamedPipeClientStream? Pipe { get; set; } = pipe;
    public StreamReader? Reader { get; set; } = reader;
    public StreamWriter? Writer { get; set; } = writer;

}


public class PipeClient
{

    private Dictionary<string, Connection> connections = new();

    public event EventHandler<PipeMessageEventArgs>? MessageReceived;

    public void Connect(string moduleName, string pipeName = "TartaMessagePipe")
    {
        var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        pipe.Connect();

        var writer = new StreamWriter(pipe)
        {
            AutoFlush = true
        };

        writer.WriteLine(moduleName);

        var reader = new StreamReader(pipe);
        var connection = new Connection(pipe, reader, writer);

        // Adding the connection object as a key pair val in connection with name being the key
        connections[pipeName] = connection;
        _ = Task.Run(() => ListenForMessages(pipeName, connection));
    }

    private void ListenForMessages(string pipeName, Connection connection)
    {
        while (true)
        {
            var recievedLine = connection.Reader.ReadLine();
            if (recievedLine == null)
                continue;

            try
            {
                // Extracting the message propertier and putting them into an object
                var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<TartaMessage>(recievedLine);
                //Invoking the eventhandler by raising an event
                MessageReceived?.Invoke(this, new PipeMessageEventArgs(pipeName, receivedMessage));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    }

    public void SendMessage(string type, string subCategory, string sender, string recipient, string payload, string pipeName = "TartaMessagePipe")
    {
        if (!connections.TryGetValue(pipeName, out var connection))
            throw new InvalidOperationException("Not connected.");

        var message = new TartaMessage(type, subCategory, sender, recipient, payload.Replace("\n", "").Replace("\r", ""));
        var json_formatted_message = System.Text.Json.JsonSerializer.Serialize(message);
        connection.Writer.WriteLine(json_formatted_message);
    }
}