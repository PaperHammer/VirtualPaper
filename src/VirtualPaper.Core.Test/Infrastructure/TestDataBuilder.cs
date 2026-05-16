using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Cores;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Core.Test.Infrastructure {
    // WpPlayerData
    // Tests/Infrastructure/TestDataBuilder.cs
    internal static class TestDataBuilder {
        /// <summary>
        /// 创建一个合法的 IWpPlayerData Mock。
        /// 内部会生成一个临时文件作为 FilePath；该文件路径会被追加到 <paramref name="tempFilesToCleanup"/>，
        /// 调用方应在 [TestCleanup] 中遍历并删除列表中的文件。
        /// </summary>
        public static Mock<IWpPlayerData> CreateValidPlayerData(
            List<string> tempFilesToCleanup,
            RuntimeType rtype = RuntimeType.RImage,
            string wpId = "wp_test_001") {
            var mock = new Mock<IWpPlayerData>();
            var tempFile = CreateTempFile(tempFilesToCleanup);
            mock.Setup(d => d.WallpaperUid).Returns(wpId);
            mock.Setup(d => d.FilePath).Returns(tempFile);
            mock.Setup(d => d.FolderPath).Returns(Path.GetTempPath());
            mock.Setup(d => d.RType).Returns(rtype);
            mock.Setup(d => d.ThumbnailPath).Returns(string.Empty);
            mock.Setup(d => d.WpEffectFilePathUsing).Returns(string.Empty);
            mock.Setup(d => d.Arrangement)
                .Returns(WallpaperArrangement.Per);
            return mock;
        }

        public static Mock<IWpPlayer> CreateWpPlayer(
            IWpPlayerData data,
            IMonitor monitor,
            bool startSuccess = true) {
            var mock = new Mock<IWpPlayer>();
            mock.Setup(p => p.Data).Returns(data);
            mock.Setup(p => p.Monitor).Returns(monitor);
            mock.Setup(p => p.IsExited).Returns(false);
            mock.Setup(p => p.ProcWindowHandle).Returns(new nint(0xABCD));
            mock.Setup(p => p.ShowAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(startSuccess);
            mock.Setup(p => p.Proc).Returns(Process.GetCurrentProcess());

            EventHandler? closingHandler = null;
            mock.SetupGet(p => p.Closing).Returns(() => closingHandler);
            mock.SetupSet(p => p.Closing = It.IsAny<EventHandler?>())
                .Callback<EventHandler?>(h => closingHandler = h);
            mock.Setup(p => p.Close())
                .Callback(() => {
                    closingHandler?.Invoke(mock.Object, EventArgs.Empty);
                });

            return mock;
        }

        /// <summary>
        /// 清理由 <see cref="CreateValidPlayerData"/> 创建的临时文件。
        /// 请在 [TestCleanup] 中调用，传入同一个 list 实例。
        /// </summary>
        public static void CleanupTempFiles(List<string> tempFilesToCleanup) {
            foreach (var path in tempFilesToCleanup) {
                try { File.Delete(path); } catch { /* 清理失败不影响测试结果 */ }
            }
            tempFilesToCleanup.Clear();
        }

        private static string CreateTempFile(List<string> tempFilesToCleanup) {
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, new byte[100]); // 确保 File.Exists 为 true
            tempFilesToCleanup.Add(path);
            return path;
        }
    }
}
