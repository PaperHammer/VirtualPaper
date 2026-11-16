using OpenCvSharp;
using VirtualPaper.ML.Inference;
using VirtualPaper.ML.SuperResolution;

namespace VirtualPaper.ML.Test.T_SuperResolution;

[TestClass]
public class SuperResolutionTilingTests {
    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(479, 33)]
    [DataRow(480, 480)]
    [DataRow(481, 33)]
    [DataRow(496, 35)]
    [DataRow(497, 33)]
    [DataRow(961, 49)]
    [DataRow(33, 481)]
    [DataRow(35, 496)]
    [DataRow(49, 961)]
    [DataRow(481, 496)]
    [DataRow(961, 961)]
    public void OutputRegions_OwnEveryPixelExactlyOnce(int width, int height) {
        var owners = new byte[width * height];
        foreach (var tile in Realesrgan.BuildTiles(width, height)) {
            Assert.IsGreaterThan(0, tile.OutputWidth);
            Assert.IsGreaterThan(0, tile.OutputHeight);
            Assert.IsTrue(tile.OutputX >= 0 && tile.OutputY >= 0
                && tile.OutputX + tile.OutputWidth <= width && tile.OutputY + tile.OutputHeight <= height);
            for (int y = tile.OutputY; y < tile.OutputY + tile.OutputHeight; y++)
                for (int x = tile.OutputX; x < tile.OutputX + tile.OutputWidth; x++) owners[y * width + x]++;
        }
        Assert.AreEqual(0, owners.Count(count => count != 1), "Output regions must have no holes or overlapping writes.");
    }

    [TestMethod]
    [DataRow(481, 33)]
    [DataRow(496, 35)]
    [DataRow(961, 49)]
    [DataRow(33, 481)]
    [DataRow(35, 496)]
    [DataRow(49, 961)]
    [DataRow(481, 496)]
    public void IdentityGraph_TiledOutputPreservesPatternAndIsRepeatable(int width, int height) {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vp-tile-" + Guid.NewGuid().ToString("N"))).FullName;
        try {
            // Self-authored float[1,3,512,512] Identity graph, IR 8/opset 13; no downloaded model weights.
            string modelPath = Path.Combine(root, "identity.onnx");
            File.WriteAllBytes(modelPath, Convert.FromBase64String(
                "CAgSElZpcnR1YWxQYXBlciB0ZXN0czp9ChkKBWlucHV0EgZvdXRwdXQiCElkZW50aXR5EhlWaXJ0dWFsUGFwZXIuVGlsZUlkZW50aXR5WiEKBWlucHV0EhgKFggBEhIKAggBCgIIAwoDCIAECgMIgARiIgoGb3V0cHV0EhgKFggBEhIKAggBCgIIAwoDCIAECgMIgARCAhAN"));
            string input = Path.Combine(root, "pattern.png"), output = Path.Combine(root, "output.png");
            using var pattern = new Mat(height, width, MatType.CV_8UC3);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) pattern.Set(y, x,
                    new Vec3b((byte)((x * 17 + y * 13) % 256), (byte)((x + y * 7) % 256), (byte)((x * 3 + y) % 256)));
            pattern.SaveImage(input);
            byte[] original = File.ReadAllBytes(input);
            byte[]? previousOutput = null;
            using var model = new Realesrgan(new InferenceDevicePolicy(false));
            model.LoadModel(modelPath);
            for (int run = 0; run < 3; run++) {
                Assert.AreEqual(output, model.RunAndSave(input, output, (uint)width, (uint)height));
                using var result = Cv2.ImRead(output);
                Assert.AreEqual(pattern.Size(), result.Size());
                using var difference = new Mat();
                Cv2.Absdiff(pattern, result, difference);
                using var channels = difference.Reshape(1);
                Cv2.MinMaxLoc(channels, out _, out double maxDifference);
                Assert.IsLessThanOrEqualTo(1d, maxDifference, "Tiling shifted, dropped or changed source pixels.");
                byte[] current = File.ReadAllBytes(output);
                if (previousOutput != null) CollectionAssert.AreEqual(previousOutput, current);
                previousOutput = current;
            }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
