using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace VirtualPaper.Cores.TrayControl {
    public class TrayCommand() {
        public async void SendMsgToUI(string msg) {
            try {
                using var client = new NamedPipeClientStream("localhost", "TRAY_CMD", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None);
                await client.ConnectAsync();

                using var writer = new StreamWriter(client);
                writer.AutoFlush = true;
                writer.WriteLine(msg);
                client.WaitForPipeDrain();
            }
            catch (Exception ex) {
                App.Log.Error($"[PipeClient] Exception: {ex.Message}");
            }
        }
    }
}
