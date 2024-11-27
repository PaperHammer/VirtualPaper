using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.ML.DepthEstimate.Models;

namespace VirtualPaper.ML.DepthEstimate {
    public class MiDaS : IDisposable {
        static MiDaS() {
            _modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Models",
                Utils.Fileds.ModelName);
            LoadModel(_modelPath);
        }

        public static ModelOutput Run(string imagePath) {
            if (string.IsNullOrEmpty(_modelPath))
                throw new FileNotFoundException("ONNX file not provided");

            if (!File.Exists(imagePath))
                throw new FileNotFoundException(imagePath);

            using var image = new Mat(imagePath, ImreadModes.AnyColor);
            if (image.Empty())
                throw new ArgumentException("Failed to load the image.");

            var inputModel = new ModelInput(imagePath, image.Width, image.Height);
            // Resize the image
            Cv2.Resize(image, image, new Size(_targetWidth, _targetHeight), (double)InterpolationFlags.Linear);

            // Convert BGR to RGB and normalize
            var rgbImage = new Mat();
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

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_modelName, dt),
            };

            using var results = _session.Run(inputs);
            var outputModel = results[0].AsEnumerable<float>().ToArray();
            var normalisedOutput = NormaliseOutput(outputModel);

            return new ModelOutput(normalisedOutput, _targetWidth, _targetHeight, inputModel.Width, inputModel.Height);
        }

        public static string SaveDepthMap(
            float[] normalisedOutput, 
            int width, 
            int height, 
            int originalWidth, 
            int originalHeight, 
            string outputFolder) {
            string outputFilePath = Path.Combine(outputFolder, Utils.Fileds.OutputFileName);

            using Mat depthMap = new(height, width, MatType.CV_8UC1); // 使用 CV_8UC1 代表 1 个 8 位通道
                                                                      // 将归一化后的深度数据复制到 Mat 中
            for (int i = 0; i < normalisedOutput.Length; i++) {
                // 计算像素位置
                int x = i % width;
                int y = i / width;

                // 将归一化的浮点数值映射到 0-255 的整数范围
                byte value = (byte)(normalisedOutput[i] * 255);

                // 设置每个通道的值
                depthMap.At<Vec3b>(y, x)[0] = value; // B
                depthMap.At<Vec3b>(y, x)[1] = value; // G
                depthMap.At<Vec3b>(y, x)[2] = value; // R
            }

            Cv2.Resize(depthMap, depthMap, new Size(originalWidth, originalHeight), (double)InterpolationFlags.Linear);

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

        private static float[] NormaliseOutput(float[] data) {
            var depthMax = data.Max();
            var depthMin = data.Min();
            var depthRange = depthMax - depthMin;

            var normalisedOutput = data.Select(d => (d - depthMin) / depthRange)
                .Select(n => ((1f - n) * 0f + n * 1f)).ToArray();

            return normalisedOutput;
        }

        private static void LoadModel(string modelPath) {
            _session?.Dispose();
            _session = new InferenceSession(modelPath);
            Debug.WriteLine($"Model version: {_session.ModelMetadata.Version}");

            _modelName = _session.InputMetadata.Keys.First();
            _targetWidth = _session.InputMetadata[_modelName].Dimensions[2];
            _targetHeight = _session.InputMetadata[_modelName].Dimensions[3];
        }

        #region
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _session?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _isDisposed = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~MiDaS()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private static InferenceSession? _session; // 执行模型推理
        private static string _modelName = string.Empty;
        private static string _modelPath = string.Empty;
        private static int _targetWidth, _targetHeight;
    }
}
