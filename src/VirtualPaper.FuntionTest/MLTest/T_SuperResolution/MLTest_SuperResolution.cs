using System.Diagnostics;
using System.IO;
using VirtualPaper.ML.SuperResolution;
using Size = OpenCvSharp.Size;

namespace VirtualPaper.FuntionTest.MLTest.T_SuperResolution {
    internal class MLTest_SuperResolution {

        /// <summary>
        /// 独立测试超分辨率模型能力
        /// </summary>
        public static void RunStandaloneTest() {
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
            if (!Directory.Exists(testDir)) Directory.CreateDirectory(testDir);

            string inputPath = Path.Combine(testDir, "low_res_test.jpg");
            string outputPath = Path.Combine(testDir, "high_res_test.jpg");

            if (!File.Exists(inputPath)) {
                Console.WriteLine($"[缺少测试文件] 请在以下目录放入用于超分测试的低清图片 'low_res_test.jpg'：\n{testDir}");
                return;
            }

            Console.WriteLine("========== 开始 Real-ESRGAN 独立测试 ==========");

            // 假设我们测试想将这张图片超分还原到标准的 1920x1080 尺寸
            Size targetSize = new Size(1920, 1080);

            Stopwatch sw = Stopwatch.StartNew();
            using (var esrgan = new Realesrgan()) {
                Console.WriteLine($"正在超分并缩放至确切尺寸: {targetSize.Width}x{targetSize.Height}");
                esrgan.Upscale(inputPath, outputPath, targetSize);
            }
            sw.Stop();

            Console.WriteLine($"✅ [测试成功] 独立超分结束！");
            Console.WriteLine($"⏱️ 耗时: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"📁 查看结果: {outputPath}");
        }
    }
}