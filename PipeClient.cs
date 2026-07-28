using System.IO.Pipes;

public class TartaMessage(string type, string subCategory, string sender, string recipient, string payload) : EventArgs
{
    public string Type { get; } = type;
    public string SubCategory { get; } = subCategory;
    public string Sender { get; } = sender;
    public string Recipient { get; } = recipient;
    public string Payload { get; } = payload;
}
public class PipeClient
{
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    public event EventHandler<TartaMessage>? MessageReceived;

    public void Connect(string moduleName)
    {
        _pipe = new NamedPipeClientStream(
            ".",
            "TartaMessagePipe",
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        _pipe.Connect();

        _writer = new StreamWriter(_pipe)
        {
            AutoFlush = true
        };

        _writer.WriteLine(moduleName);

        // Listen (forever) for messages from the server
        _reader = new StreamReader(_pipe);
        _ = Task.Run(() => ListenForMessages());
    }

    private void ListenForMessages()
    {
        while (true)
        { 
            var recievedLine = _reader.ReadLine();
            if (recievedLine == null)
                continue;

            try
            {
                // Extracting the message propertier and putting them into an object
                var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<TartaMessage>(recievedLine);
                //Invoking the eventhandler by raising an event
                MessageReceived?.Invoke(this, receivedMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    }

    public void SendMessage(string type, string subCategory, string sender, string recipient, string payload)
    {
        if (_writer == null)
            throw new InvalidOperationException("Not connected.");

        var message = new TartaMessage(type, subCategory, sender, recipient, payload.Replace("\n", "").Replace("\r", ""));
        var json_formatted_message = System.Text.Json.JsonSerializer.Serialize(message);
        _writer.WriteLine(json_formatted_message);
    }
}