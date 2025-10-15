using System.Diagnostics;
using System.IO;
using System.Text;

namespace VirtualPaper.FuntionTest.ShaderTest
{
    public class ShaderTest_Complier
    {
        /// <summary>
        /// 编译指定 HLSL 文件为 .cso。
        /// </summary>
        /// <param name="hlslPath">HLSL 文件完整路径</param>
        /// <param name="entryPoint">入口函数（默认 main）</param>
        /// <param name="profile">着色器模型（默认 ps_4_0）</param>
        /// <param name="dxcPath">dxc.exe 路径，留空表示使用系统 PATH 中的</param>
        /// <returns>编译输出的 .cso 文件路径</returns>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static string Compile(
            string hlslPath,
            string entryPoint = "main",
            string profile = "ps_4_0",
            string? dxcPath = null) {
            if (!File.Exists(hlslPath))
                throw new FileNotFoundException("HLSL 文件不存在", hlslPath);

            dxcPath ??= "dxc.exe";
            string outputPath = Path.ChangeExtension(hlslPath, ".cso");

            var psi = new ProcessStartInfo {
                FileName = dxcPath,
                Arguments = $"-T {profile} -E {entryPoint} -Fo \"{outputPath}\" \"{hlslPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动 dxc 编译进程。");

            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(outputPath)) {
                throw new InvalidOperationException(
                    $"Shader 编译失败:\n" +
                    $"File: {Path.GetFileName(hlslPath)}\n" +
                    $"ExitCode: {process.ExitCode}\n" +
                    $"Error:\n{stdErr}\n" +
                    $"Output:\n{stdOut}"
                );
            }

            return outputPath;
        }

        /// <summary>
        /// 编译并直接读取为字节数组。
        /// </summary>
        public static byte[] CompileToBytes(
            string hlslPath,
            string entryPoint = "main",
            string profile = "ps_4_0",
            string? dxcPath = null) {
            string outputPath = Compile(hlslPath, entryPoint, profile, dxcPath);
            return File.ReadAllBytes(outputPath);
        }

        /// <summary>
        /// 简单单元测试运行（可直接执行）
        /// </summary>
        public static void RunTest() {
            string shaderPath = "D:\\Virtuals\\VirtualPaper\\src\\VirtualPaper\\bin\\Debug\\net8.0-windows10.0.19041.0\\Plugins\\Shader\\Shaders\\AlphaFadeErase.hlsl";

            Console.WriteLine($"[INFO] Compiling shader: {shaderPath}");

            try {
                string output = Compile(shaderPath);
                Console.WriteLine($"✅ 编译成功: {output}");
            }
            catch (Exception ex) {
                Console.WriteLine($"❌ 编译失败: {ex.Message}");
            }
        }
    }
}
