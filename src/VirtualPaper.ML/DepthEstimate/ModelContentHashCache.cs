using System.Buffers;
using System.Security.Cryptography;

namespace VirtualPaper.ML.DepthEstimate;

/// <summary>仅保存最近一份模型指纹；调用方持有文件读锁并串行访问。</summary>
internal sealed class ModelContentHashCache {
    internal string GetHash(FileStream stream, CancellationToken ct) {
        ct.ThrowIfCancellationRequested();
        var file = new FileInfo(stream.Name);
        var stamp = new FileStamp(file.FullName, stream.Length, file.LastWriteTimeUtc, file.CreationTimeUtc);
        if (_cached is { } cached && cached.Stamp == stamp) return cached.Hash;

        string hash = ComputeHash(stream, ct);
        _cached = new(stamp, hash);
        return hash;
    }

    internal static string ComputeHash(Stream stream, CancellationToken ct) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try {
            int count;
            while ((count = stream.Read(buffer, 0, 81920)) > 0) {
                ct.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, count);
            }
            ct.ThrowIfCancellationRequested();
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private sealed record FileStamp(string Path, long Length, DateTime LastWrite, DateTime Created);
    private sealed record Entry(FileStamp Stamp, string Hash);
    private Entry? _cached;
}
