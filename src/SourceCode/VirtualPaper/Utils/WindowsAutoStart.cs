using NLog;
using System.IO;
using System.Reflection;
using System.Windows;
using VirtualPaper.Common;
using Windows.ApplicationModel;
using MessageBox = System.Windows.MessageBox;

namespace VirtualPaper.Utils
{
    public static class WindowsAutoStart
    {
        public async static Task SetAutoStart(bool isAutoStart)
        {
            if (Constants.ApplicationType.IsMSIX)
            {
                await SetAutoStartTask(isAutoStart);
            }
            else
            {
                SetAutoStartRegistry(isAutoStart);
            }
        }

        /// <summary>
        /// Adds startup entry in registry under application name "virtualpaperwpf", current user ONLY. (Does not require admin rights).
        /// </summary>
        /// <param name="setAutoStart">Add or delete entry.</param>
        private static void SetAutoStartRegistry(bool setAutoStart = false)
        {
            Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            Assembly curAssembly = Assembly.GetExecutingAssembly();
            try
            {
                if (setAutoStart)
                {
                    key?.SetValue(curAssembly.GetName().Name, "\"" + Path.ChangeExtension(curAssembly.Location, ".exe") + "\"");
                }
                else
                {
                    key?.DeleteValue(curAssembly.GetName().Name, false);
                }
            }
            finally
            {
                key?.Close();
            }
        }

        //ref: https://docs.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-19041
        private async static Task SetAutoStartTask(bool setStartup = false)
        {
            // Pass the task ID you specified in the appxmanifest file
            StartupTask startupTask = await StartupTask.GetAsync("AppStartup");
            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    _logger.Info("Startup is disabled");
                    // Task is disabled but can be enabled.
                    // ensure that you are on a UI thread when you call RequestEnableAsync()
                    if (setStartup)
                    {
                        StartupTaskState newState = await startupTask.RequestEnableAsync();
                        _logger.Info("Request to enable startup " + newState);
                    }
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    if (setStartup)
                    {
                        await Task.Run(() => MessageBox.Show("You have disabled this app's ability to run " +
                            "as soon as you sign in, but if you change your mind, " +
                            "you can enable this in the Startup tab in Task Manager.",
                            "Virtual Paper",
                            MessageBoxButton.OK));
                    }
                    break;
                case StartupTaskState.DisabledByPolicy:
                    _logger.Error("Startup disabled by group policy, or not supported on this device");
                    break;
                case StartupTaskState.Enabled:
                    _logger.Info("Startup is enabled.");
                    if (!setStartup)
                    {
                        startupTask.Disable();
                        _logger.Info("Request to disable startup");
                    }
                    break;
                default:
                    if (setStartup)
                    {
                        _logger.Info("Startup state default, possibly different value.");
                        StartupTaskState newState = await startupTask.RequestEnableAsync();
                        _logger.Info("Request to enable startup " + newState);
                    }
                    break;
            }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
