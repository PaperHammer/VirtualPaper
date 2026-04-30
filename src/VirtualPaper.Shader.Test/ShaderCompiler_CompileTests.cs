using System.Diagnostics;
using VirtualPaper.Shader.Test.Infrastructure;

namespace VirtualPaper.Shader.Test {
    [TestClass]
    [TestCategory("Integration")]
    [DoNotParallelize]
    public class ShaderCompiler_CompileTests {
        private static string? _classSkipReason;
        private string _outputDir = null!;

        public required TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassSetup(TestContext context) {
            if (!ShaderTestConfig.IsFxcAvailable()) {
                _classSkipReason = $"fxc.exe not available at: {ShaderTestConfig.FxcPath}";
                return;
            }

            if (!ShaderTestConfig.IsShaderSourceDirAvailable()) {
                _classSkipReason = $"Shader source directory not found: {ShaderTestConfig.ShaderSourceDir}";
                return;
            }

            if (!File.Exists(Path.Combine(ShaderTestConfig.ShaderIncludeDir, "d2d1effecthelpers.hlsli"))) {
                _classSkipReason = $"d2d1effecthelpers.hlsli not found in: {ShaderTestConfig.ShaderIncludeDir}";
            }
        }

        [TestInitialize]
        public void Setup() {
            if (_classSkipReason is not null)
                Assert.Inconclusive(_classSkipReason);

            _outputDir = Path.Combine(
                ShaderTestConfig.CompileOutputDir,
                $"run_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_outputDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_outputDir)) {
                try { Directory.Delete(_outputDir, recursive: true); }
                catch { /* 清理失败不影响测试结果 */ }
            }
        }

        // ── 动态测试数据 ─────────────────────────────────────────────

        public static IEnumerable<object[]> AllShaderTypes =>
            Enum.GetValues<ShaderType>()
                .Where(t => t != ShaderType.None)
                .Select(t => new object[] { t });

        // ── 逐类型编译测试 ───────────────────────────────────────────

        [TestMethod]
        [DynamicData(nameof(AllShaderTypes))]
        [Description("每个 ShaderType 对应的 HLSL 文件应能被 fxc 成功编译，生成 .bin 文件")]
        public void Compile_EachShaderType_Succeeds(ShaderType type) {
            string tempDir = PrepareWorkDir(type, out string? skipReason);
            if (skipReason is not null)
                Assert.Inconclusive(skipReason);

            string binPath = RunBat(tempDir, out int exitCode, out string stdout, out string stderr);

            TestContext.WriteLine($"ExitCode : {exitCode}");
            TestContext.WriteLine($"stdout   :\n{stdout}");
            TestContext.WriteLine($"stderr   :\n{stderr}");

            Assert.AreEqual(0, exitCode,
                $"Bat exited with {exitCode}.\nstderr: {stderr}");

            Assert.IsTrue(File.Exists(binPath),
                $".bin not generated for {type}: {binPath}");

            Assert.IsGreaterThan(0, new FileInfo(binPath).Length, $"Compiled .bin is empty for {type}");
        }

        [TestMethod]
        [DynamicData(nameof(AllShaderTypes))]
        [Description("fxc 编译输出的 .bin 文件应以 DXBC magic number 开头")]
        public void Compile_EachShaderType_OutputIsDxbc(ShaderType type) {
            string tempDir = PrepareWorkDir(type, out string? skipReason);
            if (skipReason is not null)
                Assert.Inconclusive(skipReason);

            string binPath = RunBat(tempDir, out int exitCode, out _, out string stderr);

            Assert.AreEqual(0, exitCode,
                $"Bat exited with {exitCode}.\nstderr: {stderr}");

            byte[] bytes = File.ReadAllBytes(binPath);

            // DXBC 文件头 magic: 44 58 42 43
            Assert.IsGreaterThanOrEqualTo(4, bytes.Length, "BIN file too small");
            Assert.AreEqual(0x44, bytes[0], "DXBC magic[0]");
            Assert.AreEqual(0x58, bytes[1], "DXBC magic[1]");
            Assert.AreEqual(0x42, bytes[2], "DXBC magic[2]");
            Assert.AreEqual(0x43, bytes[3], "DXBC magic[3]");
        }

        // ── 私有辅助 ─────────────────────────────────────────────────

        /// <summary>
        /// 把对应 ShaderType 的 HLSL 和 bat 脚本复制到独立临时目录。
        /// skipReason 非 null 表示应 Inconclusive。
        /// </summary>
        private string PrepareWorkDir(ShaderType type, out string? skipReason) {
            string hlslName = Path.ChangeExtension(ShaderTypeManager.GetShaderName(type), ".hlsl");
            string srcHlsl = Path.Combine(ShaderTestConfig.ShaderSourceDir, hlslName);

            if (!File.Exists(srcHlsl)) {
                skipReason = $"HLSL source not found for {type}: {srcHlsl}";
                return _outputDir;
            }

            skipReason = null;

            string tempDir = Path.Combine(
                ShaderTestConfig.CompileOutputDir,
                $"compile_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            File.Copy(srcHlsl,
                Path.Combine(tempDir, hlslName), overwrite: true);

            File.Copy(
                Path.Combine(ShaderTestConfig.ShaderSourceDir, "_CompileShaders.cmd"),
                Path.Combine(tempDir, "_CompileShaders.cmd"), overwrite: true);

            return tempDir;
        }

        /// <summary>
        /// 在指定目录执行 _CompileShaders.cmd，返回预期的 .bin 路径。
        /// </summary>
        private static string RunBat(
            string tempDir,
            out int exitCode,
            out string stdout,
            out string stderr) {

            string batPath = Path.Combine(tempDir, "_CompileShaders.cmd");
            string fxcPath = ShaderTestConfig.FxcPath;
            string includeDir = ShaderTestConfig.ShaderIncludeDir;

            var psi = new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\" \"{fxcPath}\" \"{includeDir}\"\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = tempDir,
            };

            using var process = Process.Start(psi)!;
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            exitCode = process.ExitCode;

            string hlslName = Directory
                .EnumerateFiles(tempDir, "*.hlsl")
                .Select(Path.GetFileNameWithoutExtension)
                .FirstOrDefault() ?? string.Empty;

            return Path.Combine(tempDir, hlslName + ".bin");
        }
    }
}