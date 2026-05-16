using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Cores.TrayControl {
    //public class TrayCommand() {
    //    public async void SendMsgToUI(string msg) {
    //        try {
    //            using var client = new NamedPipeClientStream("localhost", "TRAY_CMD", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.None);
    //            await client.ConnectAsync();

    //            using var writer = new StreamWriter(client);
    //            writer.AutoFlush = true;
    //            writer.WriteLine(msg);
    //            client.WaitForPipeDrain();
    //        }
    //        catch (Exception ex) {
    //            App.Log.Error($"[PipeClient] Exception: {ex.Message}");
    //        }
    //    }
    //}
    public class TrayCommand(IPipeClientFactory pipeFactory) {
        private readonly IPipeClientFactory _pipeFactory = pipeFactory;

        // async Task 替换 async void，便于测试和异常传播
        public async Task SendMsgToUIAsync(string msg, CancellationToken ct = default) {
            try {
                using var client = _pipeFactory.Create("localhost", "TRAY_CMD");
                await client.ConnectAsync(ct);

                using var writer = client.CreateWriter();
                writer.WriteLine(msg);
                client.WaitForPipeDrain();
            }
            catch (Exception ex) {
                App.Log.Error($"[PipeClient] Exception: {ex.Message}");
            }
        }
    }
}
