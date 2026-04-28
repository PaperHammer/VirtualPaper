using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.ML.Realesrgan;

namespace VirtualPaper.ML.SuperResolution {
    public class Realesrgan : IDisposable {
        static Realesrgan() {
            _modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.ML_SuperResolution_AI_Models,
                Utils.Fields.ModelName);
            _modelPath = @"D:\repos\VirtualPaper\src\VirtualPaper\Plugins\ML\SuperResolution\ai_models\realesrgan_x4plus_dynamic.onnx";

            LoadModel(_modelPath);
        }

        public static void LoadModel(string modelPath) {
            _session?.Dispose();
            var options = new SessionOptions();
            // options.AppendExecutionProvider_CUDA(); 
            _session = new InferenceSession(modelPath, options);
            Debug.WriteLine($"Real-ESRGAN Model version: {_session.ModelMetadata.Version}");
        }

        /// <summary>
        /// 执行超分辨率放大，并精确还原到指定的尺寸
        /// </summary>
        /// <param name="inputImagePath">输入图片路径 (AdaIn风格化后的小图)</param>
        /// <param name="outputImagePath">保存路径</param>
        /// <param name="exactTargetSize">需要还原到的精确尺寸 (原图尺寸)</param>
        public void Upscale(string inputImagePath, string outputImagePath, Size exactTargetSize) {
            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");

            using Mat image = Cv2.ImRead(inputImagePath, ImreadModes.Color);
            if (image.Empty()) throw new Exception($"Can not read image: {inputImagePath}");

            // OpenCV 默认 BGR，必须转换为 RGB 对齐 AI 模型
            Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

            // 转为张量 (1, 3, H, W) 并归一化 0~1
            DenseTensor<float> inputTensor = ImageToTensor(image);

            // 构造输入，注意 Real-ESRGAN 的输入节点名称一般为 "input" 或 "x"
            // 如果报错 "Invalid Feed Name"，请使用 Netron 查看模型并修改此处的 "input"
            string actualInputName = _session.InputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(actualInputName, inputTensor)
            };

            // 执行推理
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            var outputTensor = results[0].AsTensor<float>();

            // 还原为 OpenCV 图像 (RGB转回BGR)
            using Mat srImage = TensorToImage(outputTensor);

            // 精确缩放：由于模型是固定 4 倍放大，放大后的尺寸可能比我们真正需要的尺寸大或小
            // 所以我们需要 Resize 回到业务逻辑传入的确切原图尺寸
            using Mat finalImage = new Mat();
            Cv2.Resize(srImage, finalImage, exactTargetSize, 0, 0, InterpolationFlags.Area);

            finalImage.ImWrite(outputImagePath);
        }

        #region 张量与 OpenCV 图像的转换

        private DenseTensor<float> ImageToTensor(Mat img) {
            var tensor = new DenseTensor<float>(new[] { 1, 3, img.Height, img.Width });
            var indexer = img.GetGenericIndexer<Vec3b>();

            for (int y = 0; y < img.Height; y++) {
                for (int x = 0; x < img.Width; x++) {
                    Vec3b color = indexer[y, x];
                    // 归一化到 0.0 ~ 1.0
                    tensor[0, 0, y, x] = color.Item0 / 255.0f; // Red
                    tensor[0, 1, y, x] = color.Item1 / 255.0f; // Green
                    tensor[0, 2, y, x] = color.Item2 / 255.0f; // Blue
                }
            }
            return tensor;
        }

        private Mat TensorToImage(Tensor<float> tensor) {
            int height = tensor.Dimensions[2];
            int width = tensor.Dimensions[3];

            Mat image = new Mat(height, width, MatType.CV_8UC3);
            var indexer = image.GetGenericIndexer<Vec3b>();

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    // 截断越界值并放大到 0~255
                    float r = Math.Clamp(tensor[0, 0, y, x], 0.0f, 1.0f);
                    float g = Math.Clamp(tensor[0, 1, y, x], 0.0f, 1.0f);
                    float b = Math.Clamp(tensor[0, 2, y, x], 0.0f, 1.0f);

                    // OpenCV 要求按 BGR 顺序装填内存才能正确保存
                    indexer[y, x] = new Vec3b(
                        (byte)(b * 255.0f),
                        (byte)(g * 255.0f),
                        (byte)(r * 255.0f)
                    );
                }
            }
            return image;
        }

        #endregion

        #region dispose

        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _session?.Dispose();
                    _session = null;
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        private static InferenceSession? _session;
        private readonly static string _modelPath = string.Empty;
    }
}