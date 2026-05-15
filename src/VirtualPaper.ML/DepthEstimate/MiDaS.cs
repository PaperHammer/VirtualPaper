using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.DepthEstimate.Interfaces;
using VirtualPaper.ML.DepthEstimate.Models;

namespace VirtualPaper.ML.DepthEstimate {
    public partial class MiDaS : IDepthEstimate {
        public string ModelPath { get; private set; } = null!;

        public MiDaS() { }

        public void LoadModel(string? path = null) {
            if (_isLoaded) {
                ArcLog.GetLogger<MiDaS>().Info("Model already loaded, skipping.");
                return;
            }

            ModelPath = path ?? Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                Constants.WorkingDir.ML_DepthEstimate_AI_Models,
                Utils.Fields.ModelName);

            _session?.Dispose();
            using var options = new SessionOptions();
            options.EnableCpuMemArena = false;
            options.EnableMemoryPattern = false;
            _session = new InferenceSession(ModelPath, options);
            ArcLog.GetLogger<MiDaS>().Info($"Model version: {_session.ModelMetadata.Version}");

            _modelName = _session.InputMetadata.Keys.First();
            _targetWidth = _session.InputMetadata[_modelName].Dimensions[2];
            _targetHeight = _session.InputMetadata[_modelName].Dimensions[3];
            _isLoaded = true;
        }

        public DepthEstimateModelOutput Run(string imagePath) {
            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");
            if (string.IsNullOrEmpty(ModelPath)) throw new FileNotFoundException("ONNX file not provided");
            if (!File.Exists(imagePath)) throw new FileNotFoundException(imagePath);

            using var image = new Mat(imagePath, ImreadModes.AnyColor);
            if (image.Empty()) throw new ArgumentException("Failed to load the image.");

            var inputModel = new DepthEstimateModelInput(imagePath, image.Width, image.Height);
            // Resize the image
            Cv2.Resize(image, image, new Size(_targetWidth, _targetHeight), (double)InterpolationFlags.Linear);

            // Convert BGR to RGB and normalize
            using var rgbImage = new Mat();
            Cv2.CvtColor(image, rgbImage, ColorConversionCodes.BGR2RGB);

            var dt = new DenseTensor<float>([1, 3, _targetHeight, _targetWidth]);

            for (int y = 0; y < _targetHeight; y++) {
                for (int x = 0; x < _targetWidth; x++) {
                    var pixel = rgbImage.At<Vec3b>(y, x); // BGR
                    dt[0, 0, y, x] = pixel.Item0 / 255f;
                    dt[0, 1, y, x] = pixel.Item1 / 255f;
                    dt[0, 2, y, x] = pixel.Item2 / 255f;
                }
            }

            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor(_modelName, dt),
            };

            using var results = _session.Run(inputs);
            var outputModel = results[0].AsEnumerable<float>().ToArray();
            var normalisedOutput = NormaliseOutput(outputModel);

            return new DepthEstimateModelOutput(normalisedOutput, _targetWidth, _targetHeight, inputModel.Width, inputModel.Height);
        }

        private static float[] NormaliseOutput(float[] data) {
            var depthMax = data.Max();
            var depthMin = data.Min();
            var depthRange = depthMax - depthMin;

            var normalisedOutput = data.Select(d => (d - depthMin) / depthRange)
                .Select(n => ((1f - n) * 0f + n * 1f)).ToArray();
            return normalisedOutput;
        }

        public string SaveDepthMap(DepthEstimateModelOutput modelOutput, string outputFolder) {
            string outputFilePath = Path.Combine(outputFolder, Utils.Fields.OutputFileName);

            using Mat depthMap = new(modelOutput.Height, modelOutput.Width, MatType.CV_8UC1); // 使用 CV_8UC1 代表 1 个 8 位通道
                                                                                              // 将归一化后的深度数据复制到 Mat 中
            for (int i = 0; i < modelOutput.Depth.Length; i++) {
                // 计算像素位置
                int x = i % modelOutput.Width;
                int y = i / modelOutput.Width;

                // 将归一化的浮点数值映射到 0-255 的整数范围
                byte value = (byte)(modelOutput.Depth[i] * 255);
                //// 设置每个通道的值
                //depthMap.At<Vec3b>(y, x)[0] = value; // B
                //depthMap.At<Vec3b>(y, x)[1] = value; // G
                //depthMap.At<Vec3b>(y, x)[2] = value; // R
                depthMap.Set(y, x, value);
            }

            Cv2.Resize(depthMap, depthMap, new Size(modelOutput.OriginalWidth, modelOutput.OriginalHeight), (double)InterpolationFlags.Linear);

            //// Optionally display the depth map
            //using (Window win = new("Depth Map"))
            //{
            //    win.ShowImage(depthMap);
            //    Cv2.WaitKey(0); // Wait indefinitely
            //}

            // Optionally save the depth map
            if (!string.IsNullOrEmpty(outputFilePath)) {
                depthMap.SaveImage(outputFilePath);
            }

            return outputFilePath;
        }

        #region
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _session?.Dispose();
                    _session = null;
                    _isLoaded = false;
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private InferenceSession? _session; // 执行模型推理
        private string _modelName = string.Empty;
        private int _targetWidth, _targetHeight;
        private volatile bool _isLoaded;
    }
}
