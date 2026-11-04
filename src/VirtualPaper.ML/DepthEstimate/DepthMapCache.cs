using System.Security.Cryptography;
using System.Text;
using OpenCvSharp;
using VirtualPaper.ML.DepthEstimate.Interfaces;

namespace VirtualPaper.ML.DepthEstimate;

/// <summary>Serializes CPU inference and publishes complete, content-addressed depth maps.</summary>
public static class DepthMapCache {
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string DefaultModelPath => Path.Combine(
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
        Common.Constants.WorkingDir.ML_DepthEstimate_AI_Models,
        Utils.Fields.DepthAnythingV2ModelName);

    public static async Task<string> GetOrCreateAsync(
        string imagePath, string outputFolder, Func<IDepthEstimate> factory,
        CancellationToken ct, string? modelPath = null) {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try {
            return await Task.Run(() => Generate(imagePath, outputFolder, factory,
                modelPath ?? DefaultModelPath, ct), ct).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }

    private static string Generate(string imagePath, string outputFolder,
        Func<IDepthEstimate> factory, string modelPath, CancellationToken ct) {
        ct.ThrowIfCancellationRequested();
        // Hold the source against writes until inference finishes so the key describes
        // exactly the pixels read by OpenCV. Include model contents and pipeline version.
        using var source = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var model = File.Open(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "dav2-fit518-v1:" + Hash(source, ct) + ":" + Hash(model, ct))));
        Directory.CreateDirectory(outputFolder);
        string destination = Path.Combine(outputFolder, $"depth-{key}.png");
        if (File.Exists(destination)) {
            using var cached = Cv2.ImRead(destination, ImreadModes.Grayscale);
            if (!cached.Empty()) return destination;
        }

        string staging = Path.Combine(outputFolder, $".depth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try {
            ct.ThrowIfCancellationRequested();
            using var estimator = factory();
            estimator.LoadModel(modelPath);
            ct.ThrowIfCancellationRequested();
            var result = estimator.Run(imagePath, null, ct);
            ct.ThrowIfCancellationRequested();
            string generated = estimator.SaveDepthMap(result, staging);
            ct.ThrowIfCancellationRequested();
            File.Move(generated, destination, overwrite: true);
            return destination;
        }
        finally { Directory.Delete(staging, recursive: true); }
    }

    private static string Hash(Stream stream, CancellationToken ct) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        int count;
        while ((count = stream.Read(buffer)) > 0) {
            ct.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, count);
        }
        ct.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
