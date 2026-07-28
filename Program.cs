// Use nugetPackage "dotnet add package OpenCvSharp5.Windows" to install OpenCvSharp5
using System.IO.Pipelines;
using OpenCvSharp;

const string MODULE_NAME = "VideoStreamer";

// Set up the PipeClient and connect to the server
var client = new PipeClient();

client.Connect(MODULE_NAME);

Console.WriteLine($"{MODULE_NAME} is running...");

// init camera
var camera = new VideoCapture(0, VideoCaptureAPIs.DSHOW);

if (!camera.IsOpened())
{
    throw new InvalidOperationException("The webcam coul not be opened");
}

camera.Set(VideoCaptureProperties.FrameWidth, 640);
camera.Set(VideoCaptureProperties.FrameHeight, 480);
camera.Set(VideoCaptureProperties.Fps, 30);


var frame = new Mat();
try
{
    while (true)
    {
        if (!camera.Read(frame) || frame.Empty())
        {
            continue;
        }

        Cv2.ImEncode(".jpg", frame, out var encodedFrame);
        var payload = Convert.ToBase64String(encodedFrame);

        client.SendMessage("message", "videoframe", MODULE_NAME, "VideoStreamDisplay", payload);
    }

}
catch (IOException)
{

}
