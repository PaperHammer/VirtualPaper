using System.IO.Pipes;

namespace VirtualPaper.Common.Utils.IPC
{
    public class PipeClient
    {
        public static void SendMessage(string channelName, string msg)
        {
            using var pipeClient = new NamedPipeClientStream(".", channelName, PipeDirection.Out);
            pipeClient.Connect(0);
            var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            writer.Write(msg);
            writer.Flush();
            writer.Close();
            pipeClient.Dispose();
        }
    }
}
