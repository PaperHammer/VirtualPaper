using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;

namespace VirtualPaper.ML.StyleTransfer;

internal static class AdaInModelCompatibility {
    internal static bool SupportsDirectML(string modelPath) {
        // WinAppSDK 的 UI payload 可能携带比 C# 绑定包更新的原生运行库。
        Version.TryParse(OrtEnv.Instance().GetVersionString(), out Version? runtimeVersion);
        if (!IsAffectedRuntime(runtimeVersion)) return true;
        using var graph = File.OpenRead(modelPath);
        return !RequiresCpu(Convert.ToHexString(SHA256.HashData(graph)), runtimeVersion);
    }

    // 该内置图在原生 ORT 1.20.0 / 1.23.2 的 DirectML Reshape 路径失败，CPU 可正常完成。
    // 只识别已验证的组合；新模型/新运行库仍尝试 GPU，并沿用普通故障回退。
    internal static bool RequiresCpu(string graphHash, Version? runtimeVersion) =>
        IsAffectedRuntime(runtimeVersion) && graphHash == AffectedGraph;

    private static bool IsAffectedRuntime(Version? version) => version is
        { Major: 1, Minor: 20, Build: 0 } or { Major: 1, Minor: 23, Build: 2 };

    private const string AffectedGraph = "C3407747A5FD2C54470DDCB6B965215FD80D669F5EE6D6E1516E80DF08452F87";
}
