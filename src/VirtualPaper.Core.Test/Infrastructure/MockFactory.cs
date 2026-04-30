using System.Collections.ObjectModel;
using System.Drawing;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Core.Test.Infrastructure {
    internal static class MockFactory {
        public static Mock<IUserSettingsService> CreateUserSettings(
            WallpaperArrangement arrangement = WallpaperArrangement.Per) {
            var mock = new Mock<IUserSettingsService>();

            var settings = new Mock<ISettings>();
            settings.Setup(s => s.WallpaperArrangement).Returns(arrangement);
            settings.Setup(s => s.AppName).Returns(Constants.CoreField.AppName);
            settings.Setup(s => s.AppVersion).Returns("0.4.0.1");

            mock.Setup(m => m.Settings).Returns(settings.Object);
            mock.Setup(m => m.WallpaperLayouts).Returns(new List<IWallpaperLayout>());

            return mock;
        }

        public static Mock<IMonitorManager> CreateMonitorManager(
            int monitorCount = 1) {
            var mock = new Mock<IMonitorManager>();
            var monitors = BuildMonitors(monitorCount);

            mock.Setup(m => m.Monitors).Returns(monitors);
            mock.Setup(m => m.PrimaryMonitor).Returns(monitors[0]);
            mock.Setup(m => m.MonitorExists(It.IsAny<IMonitor>())).Returns(true);
            mock.Setup(m => m.IsMultiScreen()).Returns(monitorCount > 1);

            return mock;
        }

        public static Mock<INativeService> CreateDesktopService(
            bool workerWValid = true) {
            var mock = new Mock<INativeService>();
            var fakeWorkerW = workerWValid ? new nint(0x12345) : nint.Zero;

            mock.Setup(m => m.CreateWorkerW()).Returns(fakeWorkerW);
            mock.Setup(m => m.TrySetParentWorkerW(It.IsAny<nint>(), It.IsAny<nint>()))
                .Returns(true);
            mock.Setup(m => m.SetWindowPos(
                It.IsAny<nint>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(true);

            return mock;
        }

        // -------------------------------------------------------
        private static ObservableCollection<Models.Cores.Monitor> BuildMonitors(int count) {
            var list = new ObservableCollection<Models.Cores.Monitor>();
            for (int i = 0; i < count; i++) {
                var m = new Models.Cores.Monitor {
                    DeviceId = $"MONITOR_{i}",
                    IsPrimary = i == 0,
                    Content = $"Content_{i}",
                    Bounds = new Rectangle(i * 1920, 0, 1920, 1080)
                };
                list.Add(m);
            }
            return list;
        }
    }
}
