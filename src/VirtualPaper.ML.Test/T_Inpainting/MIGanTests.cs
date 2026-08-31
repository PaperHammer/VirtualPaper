using VirtualPaper.Common;
using VirtualPaper.ML.Inpainting;
using VirtualPaper.ML.Inpainting.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_Inpainting {
    [TestClass]
    [TestCategory("Unit")]
    public class MIGanTests {
        [TestMethod]
        public void ExpandMask_SinglePixel_ExpandsAndPreservesDimensions() {
            const int width = 9;
            const int height = 7;
            var mask = new byte[width * height];
            mask[3 * width + 4] = byte.MaxValue;

            byte[] expanded = MIGan.ExpandMask(mask, width, height, 2);

            Assert.HasCount(mask.Length, expanded);
            Assert.AreEqual(byte.MaxValue, expanded[3 * width + 4]);
            Assert.AreEqual(byte.MaxValue, expanded[3 * width + 2]);
            Assert.AreEqual(byte.MinValue, expanded[0]);
            Assert.IsGreaterThan(1, expanded.Count(value => value != 0));
        }

        [TestMethod]
        public void Options_ExpansionOutsideSupportedRange_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MIGanOptions { MaskExpansionPixels = 129 }.Validate());
        }

        [TestMethod]
        public void CalculateExpansionPixels_LargeImage_UsesResolutionRatio() {
            int result = MIGan.CalculateExpansionPixels(
                5424,
                3196,
                new MIGanOptions());

            Assert.AreEqual(32, result);
        }

        [TestMethod]
        public void CalculateExpansionPixels_RelativeValueIsClamped() {
            int result = MIGan.CalculateExpansionPixels(
                12000,
                8000,
                new MIGanOptions { MaximumMaskExpansionPixels = 48 });

            Assert.AreEqual(48, result);
        }

        [TestMethod]
        public void FillSmallEnclosedHoles_FillsOnlyEnclosedRegion() {
            const int width = 12;
            const int height = 10;
            var mask = new byte[width * height];
            for (int y = 2; y <= 7; y++) {
                for (int x = 2; x <= 9; x++)
                    mask[y * width + x] = byte.MaxValue;
            }
            mask[4 * width + 5] = byte.MinValue;

            byte[] result = MIGan.FillSmallEnclosedHoles(mask, width, height, 4);

            Assert.AreEqual(byte.MaxValue, result[4 * width + 5]);
            Assert.AreEqual(byte.MinValue, result[0]);
        }

        [TestMethod]
        public void Run_WithoutLoadingModel_Throws() {
            string directory = Path.Combine(Path.GetTempPath(), $"migan_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try {
                string imagePath = TestImageHelper.CreateSolidColorJpeg(16, 16, directory);
                using var model = new MIGan();
                Assert.Throws<InvalidOperationException>(() =>
                    model.Run(imagePath, new byte[16 * 16], 16, 16));
            }
            finally {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestClass]
    [TestCategory("Integration")]
    public class MIGanIntegrationTests {
        [TestMethod]
        public void Run_LocalModel_ReturnsFullResolutionBgrPlate() {
            string modelPath = Path.Combine(
                Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                Constants.WorkingDir.ML,
                "Inpainting",
                "ai_models",
                VirtualPaper.ML.Inpainting.Utils.Fields.ModelName);
            if (!File.Exists(modelPath))
                Assert.Inconclusive($"Required local model is missing: {modelPath}");

            const int width = 96;
            const int height = 64;
            string directory = Path.Combine(Path.GetTempPath(), $"migan_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try {
                string imagePath = TestImageHelper.CreateSolidColorJpeg(width, height, directory);
                var mask = new byte[width * height];
                for (int y = 20; y < 44; y++) {
                    for (int x = 36; x < 60; x++)
                        mask[y * width + x] = byte.MaxValue;
                }

                using var model = new MIGan();
                model.LoadModel(modelPath);
                InpaintingModelOutput result = model.Run(
                    imagePath,
                    mask,
                    width,
                    height,
                    new MIGanOptions { MaskExpansionPixels = 4 });

                Assert.IsTrue(result.WasApplied);
                Assert.AreEqual(width, result.Width);
                Assert.AreEqual(height, result.Height);
                Assert.HasCount(width * height * 3, result.BgrPixels);
                Assert.HasCount(width * height, result.AppliedMask);
                Assert.AreEqual(4, result.SafetyMarginPixels);
                Assert.IsTrue(result.BgrPixels.Any(value => value != 0));
            }
            finally {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
