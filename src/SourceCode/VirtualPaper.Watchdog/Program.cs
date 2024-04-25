using System.Diagnostics;
using VirtualPaper.Common.Utils.PInvoke;

//TODO: Kill UI program also.
namespace VirtualPaper.Watchdog
{
    /// <summary>
    /// Kills external application type wallpapers in the event main pgm is killed by taskmanager/other programs.
    /// <br>Commands:</br> 
    /// <br>ADD/RMV {pid} - Add/remove program from watchlist.</br>
    /// <br>CLR - Clear watchlist.</br>
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            int parentProcessId;
            Process parentProcess;

            if (args.Length == 1)
            {
                try
                {
                    parentProcessId = Convert.ToInt32(args[0], 10);
                }
                catch
                {
                    //ERROR: converting toint
                    return;
                }
            }
            else
            {
                //"Incorrent no of arguments."
                return;
            }

            try
            {
                parentProcess = Process.GetProcessById(parentProcessId);
            }
            catch
            {
                //getting processname failure!
                return;
            }

            StdInListener();
            parentProcess.WaitForExit();

            foreach (var item in _activePrograms)
            {
                try
                {
                    Process.GetProcessById(item).Kill();
                }
                catch { }
            }

            foreach (var item in Process.GetProcessesByName("VirtualPaper.UI"))
            {
                try
                {
                    item.Kill();
                }
                catch { }
            }

            //force refresh desktop.
            _ = Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, null, Native.SPIF_UPDATEINIFILE);
        }

        /// <summary>
        /// std I/O redirect.
        /// </summary>
        private static async void StdInListener()
        {
            try
            {
                await Task.Run(async () =>
                {
                    while (true)
                    {
                        var msg = await Console.In.ReadLineAsync();
                        var args = msg.Split(' ');
                        if (args[0].Equals("CLR", StringComparison.OrdinalIgnoreCase))
                        {
                            _activePrograms.Clear();
                        }
                        else if (args[0].Equals("ADD", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(args[1], out int value))
                            {
                                _activePrograms.Add(value);
                            }
                        }
                        else if (args[0].Equals("RMV", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(args[1], out int value))
                            {
                                _ = _activePrograms.Remove(value);
                            }
                        }
                    }
                });
            }
            catch { }
        }

        private static readonly List<int> _activePrograms = [];
    }
}
