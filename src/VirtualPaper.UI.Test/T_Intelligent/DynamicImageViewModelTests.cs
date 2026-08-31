using VirtualPaper.Common.Utils;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_Intelligent {
    [TestClass]
    public class DynamicImageAddTaskViewModelTests {
        [TestMethod]
        public void Constructor_DefaultsToBalancedAndDisabled() {
            var viewModel = new DynamicImageAddTaskViewModel();

            Assert.IsTrue(viewModel.IsBalanced);
            Assert.IsFalse(viewModel.IsHighQuality);
            Assert.IsFalse(viewModel.IsNextEnable);
        }

        [TestMethod]
        public async Task Confirm_ProducesDynamicImageDataWithSelectedQuality() {
            var completion = new ResettableCompletionSource<IIntelliData?>();
            var viewModel = new DynamicImageAddTaskViewModel {
                IntelligentCTS = completion,
                IsHighQuality = true
            };
            viewModel.SetSourceForTest("image.png", "1 MB", ".png", 1920, 1080);

            await viewModel.OnNextStepClickedAsync();
            IIntelliData? result = await completion.Task;

            Assert.IsInstanceOfType<DynamicImageData>(result);
            var data = (DynamicImageData)result!;
            Assert.AreEqual(DynamicImageQuality.High, data.Quality);
            Assert.AreEqual((uint)1920, data.Width);
            Assert.AreEqual((uint)1080, data.Height);
        }

        [TestMethod]
        public void Clean_ResetsSourceAndQuality() {
            var viewModel = new DynamicImageAddTaskViewModel { IsHighQuality = true };
            viewModel.SetSourceForTest("image.png", "1 MB", ".png", 10, 10);

            viewModel.Clean();

            Assert.IsNull(viewModel.SourceFilePath);
            Assert.IsFalse(viewModel.IsHighQuality);
            Assert.IsFalse(viewModel.IsNextEnable);
        }
    }

    [TestClass]
    public class DynamicImageQualityOptionsTests {
        [TestMethod]
        public void Balanced_UsesCpuFriendlySettings() {
            var options = DynamicImageViewModel.CreateOptions(DynamicImageQuality.Balanced);

            Assert.AreEqual(640, options.Detection.InputWidth);
            Assert.AreEqual(518, options.Depth.InputSize);
            Assert.AreEqual(8, options.Fusion.MaxObjects);
            Assert.AreEqual(8, options.Segmentation.MaxBoxes);
        }

        [TestMethod]
        public void High_UsesHigherResolutionSettings() {
            var options = DynamicImageViewModel.CreateOptions(DynamicImageQuality.High);

            Assert.AreEqual(960, options.Detection.InputWidth);
            Assert.AreEqual(686, options.Depth.InputSize);
            Assert.AreEqual(12, options.Fusion.MaxObjects);
            Assert.AreEqual(12, options.Segmentation.MaxBoxes);
        }
    }
}
