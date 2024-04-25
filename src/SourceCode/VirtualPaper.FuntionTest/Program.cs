using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Xml;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;

namespace VirtualPaper.FuuntionTest
{
    internal class Program
    {
        public static Process? Proc { get; set; }
        public static nint Handle { get; private set; }
        public static nint InputHandle { get; private set; }
        public static bool IsExited { get; private set; }
        public static bool IsLoaded { get; private set; } = false;

        static async Task Main(string[] args)
        {
            //string loadActionJson = "{\"action\":\"--load\",\"source\":\"C:\\\\Users\\\\PaperHammer\\\\Desktop\\\\img28.jpg\",\"type\":\"picture\"}";
            ////string loadActionJsox = "{\"action\":\"_load\",\"source\":\"C:\\\\Users\\\\PaperHammer\\\\Desktop\\\\img28.jpg\",\"type\":\"picture\"}";
            //string modifyActionJson = "{\"action\":\"--modify\",\"imgVal\":{\"Saturation\":{\"Type\":\"Slider\",\"Text\":\"Saturation\",\"Value\":1.0,\"Max\":10.0,\"Min\":0.0,\"Step\":0.1},\"Hue\":{\"Type\":\"Slider\",\"Text\":\"Hue\",\"Value\":0,\"Max\":359,\"Min\":0,\"Step\":1},\"Brightness\":{\"Type\":\"Slider\",\"Text\":\"Brightness\",\"Value\":1.0,\"Max\":2.0,\"Min\":0.0,\"Step\":0.1},\"Contrast\":{\"Type\":\"Slider\",\"Text\":\"Contrast\",\"Value\":1.0,\"Max\":10.0,\"Min\":0.0,\"Step\":0.1}}}";
            ////string modifyActionJsox = "{\"action\":\"_modify\",\"imgVal\":{\"Saturation\":{\"Type\":\"Slider\",\"Text\":\"Saturation\",\"Value\":1.0,\"Max\":10.0,\"Min\":0.0,\"Step\":0.1},\"Hue\":{\"Type\":\"Slider\",\"Text\":\"Hue\",\"Value\":0,\"Max\":359,\"Min\":0,\"Step\":1},\"Brightness\":{\"Type\":\"Slider\",\"Text\":\"Brightness\",\"Value\":1.0,\"Max\":2.0,\"Min\":0.0,\"Step\":0.1},\"Contrast\":{\"Type\":\"Slider\",\"Text\":\"Contrast\",\"Value\":1.0,\"Max\":10.0,\"Min\":0.0,\"Step\":0.1}}}";

            //// 创建临时文件夹（如有必要）
            //string tempFolderPath = Path.Combine(Path.GetTempPath(), "VirtualPaperActions");
            //Directory.CreateDirectory(tempFolderPath);

            //// 写入JSON到临时文件
            //string loadActionFilePath = Path.Combine(tempFolderPath, "load.json");
            //File.WriteAllText(loadActionFilePath, loadActionJson);

            //string modifyActionFilePath = Path.Combine(tempFolderPath, "modify.json");
            //File.WriteAllText(modifyActionFilePath, modifyActionJson);          

            string filePath = "C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\Library\\wallpapers\\n1j43ben.crv\\02inb0tr.oue.jpg";
            string customizePath = "C:\\Users\\PaperHammer\\AppData\\Local\\VirtualPaper\\Library\\wallpapers\\n1j43ben.crv\\WpCustomize.json";

            DirectoryInfo path = new(AppDomain.CurrentDomain.BaseDirectory);
            string workingDir = path.Parent.Parent.FullName;
            workingDir = Path.Combine(workingDir, "Plugins", "Webviewer");

            StringBuilder cmdArgs = new();
            cmdArgs.Append(" --working-dir " + "\"" + workingDir + "\"");
            cmdArgs.Append(" --file-path " + "\"" + filePath + "\"");
            cmdArgs.Append(" --wallpaper-type " + "picture");
            cmdArgs.Append(" --customize-file-path " + "\"" + customizePath + "\"");

            ProcessStartInfo start = new()
            {
                FileName = Path.Combine(workingDir, "VirtualPaper.PlayerWebView2.exe"),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                Arguments = cmdArgs.ToString(),
            };

            Process _process = new()
            {
                EnableRaisingEvents = true,
                StartInfo = start,
            };

            await Console.Out.WriteLineAsync(start.Arguments);

            Proc = _process;

            await ShowAsync();

            //Application.Run();
        }

        static async Task<bool> ShowAsync()
        {
            try
            {
                Proc.Exited += Proc_Exited;
                Proc.OutputDataReceived += Proc_OutputDataReceived;
                Proc.Start();
                Proc.BeginOutputReadLine();

                await _tcsProcessWait.Task;
                if (_tcsProcessWait.Task.Result is not null)
                    throw _tcsProcessWait.Task.Result;

                return true;
            }
            catch (Exception)
            {
                Terminate();

                return false;
            }
        }

        private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (!_isInitialized || !IsLoaded)
                {
                    IpcMessage obj;
                    try
                    {
                        obj = JsonConvert.DeserializeObject<IpcMessage>(e.Data, new JsonSerializerSettings() { Converters = { new IpcMessageConverter() } }) ?? throw new("null msg recieved");
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    if (obj.Type == MessageType.msg_hwnd)
                    {
                        Exception? error = null;
                        try
                        {
                            var handle = new IntPtr(((VirtualPaperMessageHwnd)obj).Hwnd);

                            var chrome_WidgetWin_0 = Native.FindWindowEx(handle, IntPtr.Zero, "Chrome_WidgetWin_0", null);
                            if (!chrome_WidgetWin_0.Equals(IntPtr.Zero))
                            {
                                InputHandle = Native.FindWindowEx(chrome_WidgetWin_0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
                            }
                            Handle = Proc.GetProcessWindow(true);

                            if (IntPtr.Equals(Handle, IntPtr.Zero) || IntPtr.Equals(InputHandle, IntPtr.Zero))
                            {
                                throw new Exception("Browser input/window handle NULL.");
                            }
                        }
                        catch (Exception ie)
                        {
                            error = ie;
                        }
                        finally
                        {
                            _isInitialized = true;
                            _tcsProcessWait.TrySetResult(error);
                        }
                    }
                    else if (obj.Type == MessageType.msg_wploaded)
                    {
                        IsLoaded = true;
                    }
                }
            }
        }       

        private static void Terminate()
        {
            try
            {
                Proc?.Kill();
            }
            catch { }
        }

        private static void Proc_Exited(object? sender, EventArgs e)
        {
            Proc?.Dispose();
        }

        private static readonly TaskCompletionSource<Exception> _tcsProcessWait = new();
        private static bool _isInitialized;
    }
}
