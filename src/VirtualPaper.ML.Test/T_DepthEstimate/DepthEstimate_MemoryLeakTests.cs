using System.Diagnostics;
using VirtualPaper.Common;
using VirtualPaper.ML.DepthEstimate;

namespace VirtualPaper.ML.Test.T_DepthEstimate {
    [TestClass]
    public class DepthEstimate_MemoryLeakTests {
        private string _tempDir = null!;
        private MiDaS _midas = null!;
        private string _testImagePath = null!;
        private readonly string _modelPath =
            Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory)),
                Constants.WorkingDir.ML_DepthEstimate_AI_Models,
                Utils.Fields.ModelName);

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"midas_leak_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(dir: _tempDir);

            _midas = new MiDaS();
            _midas.LoadModel(_modelPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _midas?.Dispose();

            try {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Detects overall memory leak in Run() (managed + unmanaged).
        /// 
        /// Strategy: 
        ///   - Do NOT call ForceGC during or after the test loop.
        ///   - This prevents Finalizers from masking missing Dispose calls.
        ///   - Sample memory at multiple points and check for a linear growth trend.
        /// </summary>
        [TestMethod]
        public void Run_NoMemoryLeak() {
            const int warmupCount = 10;
            const int testCount = 500;
            const long maxAllowedGrowthMB = 30;

            // Warm up: stabilize JIT, ONNX Runtime caches, thread pool, etc.
            for (int i = 0; i < warmupCount; i++) {
                _midas.Run(_testImagePath);
            }

            // One-time GC to establish a clean baseline
            StabilizeMemory();
            long memBaseline = GetPrivateMemoryMB();
            Console.WriteLine($"[Baseline] Private Memory: {memBaseline} MB");

            // Collect memory samples at intervals (NO GC during test)
            var samples = new List<(int iteration, long memoryMB)>();

            for (int i = 0; i < testCount; i++) {
                _midas.Run(_testImagePath);

                if ((i + 1) % 50 == 0) {
                    Process.GetCurrentProcess().Refresh();
                    long current = GetPrivateMemoryMB();
                    samples.Add((i + 1, current));
                    Console.WriteLine($"  [Iter {i + 1,3}] Private Memory: {current} MB");
                }
            }

            // Final measurement — still NO ForceGC, to catch real leaks
            Process.GetCurrentProcess().Refresh();
            long memFinal = GetPrivateMemoryMB();
            samples.Add((testCount, memFinal));

            long totalGrowth = memFinal - memBaseline;
            Console.WriteLine($"[Final]   Private Memory: {memFinal} MB");
            Console.WriteLine($"[Growth]  {totalGrowth} MB over {testCount} calls");
            Console.WriteLine($"[Per Call] ~{(totalGrowth * 1024.0 / testCount):F1} KB");

            // Check 1: absolute growth should be bounded
            Assert.IsLessThan(maxAllowedGrowthMB, totalGrowth, $"Memory leak detected! Grew by {totalGrowth} MB over {testCount} calls, " +
                $"approximately {(totalGrowth * 1024.0 / testCount):F1} KB per call. " +
                $"Threshold: {maxAllowedGrowthMB} MB.");

            // Check 2: detect linear growth trend (the real leak signal)
            AssertNoLinearGrowthTrend(samples, memBaseline);
        }

        /// <summary>
        /// Isolates the OpenCV portion to determine if it leaks.
        /// </summary>
        [TestMethod]
        public void Run_OpenCvOnly_NoLeak() {
            const int testCount = 500;
            const long maxAllowedGrowthMB = 15;

            // Warm up
            for (int i = 0; i < 10; i++) {
                using var img = new OpenCvSharp.Mat(_testImagePath, OpenCvSharp.ImreadModes.AnyColor);
                OpenCvSharp.Cv2.Resize(img, img, new OpenCvSharp.Size(256, 256));
                using var rgb = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.CvtColor(img, rgb, OpenCvSharp.ColorConversionCodes.BGR2RGB);
            }

            StabilizeMemory();
            long memBaseline = GetPrivateMemoryMB();
            Console.WriteLine($"[OpenCV Baseline] Private Memory: {memBaseline} MB");

            var samples = new List<(int iteration, long memoryMB)>();

            for (int i = 0; i < testCount; i++) {
                using var img = new OpenCvSharp.Mat(_testImagePath, OpenCvSharp.ImreadModes.AnyColor);
                OpenCvSharp.Cv2.Resize(img, img, new OpenCvSharp.Size(256, 256));
                using var rgb = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.CvtColor(img, rgb, OpenCvSharp.ColorConversionCodes.BGR2RGB);

                if ((i + 1) % 100 == 0) {
                    Process.GetCurrentProcess().Refresh();
                    long current = GetPrivateMemoryMB();
                    samples.Add((i + 1, current));
                    Console.WriteLine($"  [Iter {i + 1,3}] Private Memory: {current} MB");
                }
            }

            Process.GetCurrentProcess().Refresh();
            long memFinal = GetPrivateMemoryMB();
            samples.Add((testCount, memFinal));
            long growth = memFinal - memBaseline;

            Console.WriteLine($"[OpenCV Only] Growth: {growth} MB over {testCount} calls");

            Assert.IsLessThan(maxAllowedGrowthMB, growth, $"OpenCV portion leaked {growth} MB over {testCount} calls.");

            AssertNoLinearGrowthTrend(samples, memBaseline);
        }

        /// <summary>
        /// Isolates ONNX Runtime inference to determine if it leaks.
        /// </summary>
        [TestMethod]
        public void Run_OnnxOnly_NoLeak() {
            const int testCount = 500;
            const long maxAllowedGrowthMB = 15;

            var session = typeof(MiDaS)
                .GetField("_session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_midas) as Microsoft.ML.OnnxRuntime.InferenceSession;

            Assert.IsNotNull(session, "Failed to retrieve _session via reflection.");

            var modelName = session.InputMetadata.Keys.First();
            int h = session.InputMetadata[modelName].Dimensions[2];
            int w = session.InputMetadata[modelName].Dimensions[3];

            var fakeTensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>([1, 3, h, w]);

            // Warm up
            for (int i = 0; i < 10; i++) {
                var inputs = new List<Microsoft.ML.OnnxRuntime.NamedOnnxValue> {
                    Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(modelName, fakeTensor)
                };
                using var results = session.Run(inputs);
                _ = results[0].AsEnumerable<float>().ToArray();
            }

            StabilizeMemory();
            long memBaseline = GetPrivateMemoryMB();
            Console.WriteLine($"[ONNX Baseline] Private Memory: {memBaseline} MB");

            var samples = new List<(int iteration, long memoryMB)>();

            for (int i = 0; i < testCount; i++) {
                var inputs = new List<Microsoft.ML.OnnxRuntime.NamedOnnxValue> {
                    Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor(modelName, fakeTensor)
                };
                using var results = session.Run(inputs);
                _ = results[0].AsEnumerable<float>().ToArray();

                if ((i + 1) % 100 == 0) {
                    Process.GetCurrentProcess().Refresh();
                    long current = GetPrivateMemoryMB();
                    samples.Add((i + 1, current));
                    Console.WriteLine($"  [Iter {i + 1,3}] Private Memory: {current} MB");
                }
            }

            Process.GetCurrentProcess().Refresh();
            long memFinal = GetPrivateMemoryMB();
            samples.Add((testCount, memFinal));
            long growth = memFinal - memBaseline;

            Console.WriteLine($"[ONNX Only] Growth: {growth} MB over {testCount} calls");

            Assert.IsLessThan(maxAllowedGrowthMB, growth, $"ONNX Runtime portion leaked {growth} MB over {testCount} calls.");

            AssertNoLinearGrowthTrend(samples, memBaseline);
        }

        /// <summary>
        /// Detects managed heap leaks without ForceGC masking the problem.
        /// </summary>
        [TestMethod]
        public void Run_NoManagedHeapLeak() {
            const int testCount = 500;
            const long maxAllowedGrowthMB = 10;

            // Warm up
            for (int i = 0; i < 10; i++) {
                _midas.Run(_testImagePath);
            }

            // Use GC.GetTotalMemory(true) only for baseline — it forces collection once
            long managedBaseline = GC.GetTotalMemory(forceFullCollection: true);
            Console.WriteLine($"[Managed Baseline] {managedBaseline / 1024 / 1024} MB");

            var samples = new List<(int iteration, long memoryMB)>();

            for (int i = 0; i < testCount; i++) {
                _midas.Run(_testImagePath);

                if ((i + 1) % 100 == 0) {
                    // NO forceFullCollection — let GC behave naturally
                    long current = GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024;
                    samples.Add((i + 1, current));
                    Console.WriteLine($"  [Iter {i + 1,3}] Managed Heap: {current} MB");
                }
            }

            // Final: one collection to see what's truly retained
            long managedFinal = GC.GetTotalMemory(forceFullCollection: true);
            long growthMB = (managedFinal - managedBaseline) / 1024 / 1024;

            Console.WriteLine($"[Managed Final]  {managedFinal / 1024 / 1024} MB");
            Console.WriteLine($"[Managed Growth] {growthMB} MB");

            Assert.IsLessThan(maxAllowedGrowthMB, growthMB, $"Managed heap leaked {growthMB} MB over {testCount} calls.");
        }

        /// <summary>
        /// Stress test: high iteration count to amplify any small per-call leak.
        /// Runs 2000 iterations. If each call leaks even 100KB, that's ~200MB growth.
        /// </summary>
        [TestMethod]
        [Timeout(300_000, CooperativeCancellation = true)] // 5 minutes
        public void Run_StressTest_NoLeak() {
            const int warmupCount = 10;
            const int testCount = 2000;
            const long maxAllowedGrowthMB = 50;

            for (int i = 0; i < warmupCount; i++) {
                _midas.Run(_testImagePath);
            }

            StabilizeMemory();
            long memBaseline = GetPrivateMemoryMB();
            Console.WriteLine($"[Stress Baseline] Private Memory: {memBaseline} MB");

            long peakMemory = memBaseline;

            for (int i = 0; i < testCount; i++) {
                _midas.Run(_testImagePath);

                if ((i + 1) % 500 == 0) {
                    Process.GetCurrentProcess().Refresh();
                    long current = GetPrivateMemoryMB();
                    peakMemory = Math.Max(peakMemory, current);
                    Console.WriteLine($"  [Iter {i + 1,4}] Private Memory: {current} MB (peak: {peakMemory} MB)");
                }
            }

            Process.GetCurrentProcess().Refresh();
            long memFinal = GetPrivateMemoryMB();
            peakMemory = Math.Max(peakMemory, memFinal);
            long growth = memFinal - memBaseline;

            Console.WriteLine($"[Stress Final] Private Memory: {memFinal} MB");
            Console.WriteLine($"[Stress Peak]  {peakMemory} MB");
            Console.WriteLine($"[Stress Growth] {growth} MB over {testCount} calls");
            Console.WriteLine($"[Stress Per Call] ~{(growth * 1024.0 / testCount):F1} KB");

            Assert.IsLessThan(maxAllowedGrowthMB, growth, $"Stress test leaked {growth} MB over {testCount} calls " +
                $"(~{(growth * 1024.0 / testCount):F1} KB/call). Peak: {peakMemory} MB.");
        }

        #region Helpers

        /// <summary>
        /// One-time stabilization before baseline measurement.
        /// Called only ONCE before the test loop, never during or after.
        /// </summary>
        private static void StabilizeMemory() {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            Thread.Sleep(500);
            Process.GetCurrentProcess().Refresh();
        }

        private static long GetPrivateMemoryMB() {
            Process.GetCurrentProcess().Refresh();
            return Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024;
        }

        /// <summary>
        /// Uses simple linear regression to detect a consistent upward trend.
        /// A positive slope above the threshold indicates a likely leak.
        /// </summary>
        private static void AssertNoLinearGrowthTrend(
            List<(int iteration, long memoryMB)> samples,
            long baseline) {

            if (samples.Count < 3) return; // not enough data points

            // Simple linear regression: y = slope * x + intercept
            double n = samples.Count;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

            foreach (var (iteration, memoryMB) in samples) {
                double x = iteration;
                double y = memoryMB - baseline; // growth relative to baseline
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;

            // slope is MB per iteration
            double kbPerCall = slope * 1024.0;

            Console.WriteLine($"[Trend Analysis] Slope: {slope:F4} MB/iter ({kbPerCall:F1} KB/call)");
            Console.WriteLine($"[Trend Analysis] Intercept: {intercept:F1} MB");

            // Print all sample points for diagnosis
            Console.WriteLine($"[Trend Analysis] Samples:");
            foreach (var (iteration, memoryMB) in samples) {
                Console.WriteLine($"    iter={iteration}, mem={memoryMB} MB, growth={memoryMB - baseline} MB");
            }

            // If slope > 50 KB/call, it's a strong signal of a leak
            // (A 256x256 Mat is ~192KB, so leaking one per call would show ~200 KB/call)
            const double maxSlopeKBPerCall = 50.0;

            Assert.IsLessThan(maxSlopeKBPerCall, kbPerCall, $"Linear growth trend detected! " +
                $"Slope: {kbPerCall:F1} KB/call (threshold: {maxSlopeKBPerCall} KB/call). " +
                $"This strongly suggests a memory leak.");
        }

        #endregion
    }
}