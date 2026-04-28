using System.Diagnostics;
using System.IO;
using OpenCvSharp;
using VirtualPaper.ML.StyleTransfer;
using VirtualPaper.ML.SuperResolution;
using Size = OpenCvSharp.Size;

namespace VirtualPaper.FuntionTest.MLTest.T_StyleTransfer {
    [TestClass]
    public class MLTest_StyleTransfer {
        /// <summary>
        /// 完整工作流测试：获取原图尺寸 -> 风格迁移(降维) -> 超分还原尺寸
        /// </summary>
        [TestMethod]
        public void RunPipelineTest() {
            // 1. 根据你的目录结构，构建基础路径
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MLTest", "T_StyleTransfer", "data");
            string contentDir = Path.Combine(baseDir, "content");
            string styleDir = Path.Combine(baseDir, "style");
            string outputDir = Path.Combine(baseDir, "output");

            // 确保目录结构存在，如果不存在则自动创建
            if (!Directory.Exists(contentDir)) Directory.CreateDirectory(contentDir);
            if (!Directory.Exists(styleDir)) Directory.CreateDirectory(styleDir);
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            // 2. 定义具体的测试文件路径 (假设你的测试图叫 content.jpg 和 style.jpg)
            string contentPath = Path.Combine(contentDir, "img30.jpg");
            string stylePath = Path.Combine(styleDir, "impronte_d_artista.jpg");

            // 临时输出与最终输出存放在 output 文件夹中
            string tempAdaInOutput = Path.Combine(outputDir, "temp_adain.jpg");
            string finalOutputPath = Path.Combine(outputDir, "final_result.jpg");

            // 3. 检查测试文件是否就绪
            if (!File.Exists(contentPath) || !File.Exists(stylePath)) {
                Debug.WriteLine($"[缺少测试文件]");
                Debug.WriteLine($"请确保你已经放入了以下两张测试图片：");
                Debug.WriteLine($"内容图: {contentPath}");
                Debug.WriteLine($"风格图: {stylePath}");
                return;
            }

            Debug.WriteLine("========== 开始 AI 风格迁移混合流水线测试 ==========");
            Stopwatch totalTimer = Stopwatch.StartNew();

            // [步骤 1] 获取内容图真实尺寸 (关键步骤)
            Size originalSize;
            using (var mat = Cv2.ImRead(contentPath, ImreadModes.Color)) {
                if (mat.Empty()) throw new Exception("无法读取内容图！请检查图片是否损坏。");
                originalSize = new Size(mat.Width, mat.Height);
                Debug.WriteLine($"[1/4] 原图尺寸读取成功: {originalSize.Width}x{originalSize.Height}");
            }

            // [步骤 2] 执行风格迁移
            Debug.WriteLine("[2/4] 正在执行风格迁移 (AdaIn 降维处理)...");
            Stopwatch adainTimer = Stopwatch.StartNew();
            using (var adain = new AdaIn()) {
                // contentSize = 512, 降低推理压力并防止风格笔触细碎
                adain.TransferStyle(
                    contentImagePath: contentPath,
                    styleImagePath: stylePath,
                    outputImagePath: tempAdaInOutput, // 临时小图保存到 output 目录
                    alpha: 1.0f,
                    contentSize: 512,
                    styleSize: 512
                );
            }
            adainTimer.Stop();
            Debug.WriteLine($"      -> AdaIn 耗时: {adainTimer.ElapsedMilliseconds} ms");

            // [步骤 3] 执行超分模型并精确还原至原图大小
            Debug.WriteLine($"[3/4] 正在执行超分辨率放大 (恢复至 {originalSize.Width}x{originalSize.Height})...");
            Stopwatch srTimer = Stopwatch.StartNew();
            using (var esrgan = new Realesrgan()) {
                esrgan.Upscale(
                    inputImagePath: tempAdaInOutput,
                    outputImagePath: finalOutputPath, // 最终高清图保存到 output 目录
                    exactTargetSize: originalSize // 将原图 Size 传入，保证最终输出严丝合缝
                );
            }
            srTimer.Stop();
            Debug.WriteLine($"      -> Real-ESRGAN 耗时: {srTimer.ElapsedMilliseconds} ms");

            // [步骤 4] 清理临时小图
            Debug.WriteLine("[4/4] 清理临时文件...");
            if (File.Exists(tempAdaInOutput)) {
                File.Delete(tempAdaInOutput);
            }

            totalTimer.Stop();
            Debug.WriteLine($"\n✅ [测试成功] 完整工作流结束！");
            Debug.WriteLine($"⏱️ 总耗时: {totalTimer.ElapsedMilliseconds} ms");
            Debug.WriteLine($"📁 请查看最终结果: {finalOutputPath}");
        }
    }
}