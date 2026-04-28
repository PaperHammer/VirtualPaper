using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;

namespace VirtualPaper.ML.StyleTransfer {
    public class AdaIn : IDisposable {
        static AdaIn() {
            _modelPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.ML_StyleTransfer_AI_Models,
                Utils.Fields.ModelName);
            _modelPath = @"D:\repos\VirtualPaper\src\VirtualPaper\Plugins\ML\StyleTransfer\ai_models\adain_style_transfer.onnx";

            LoadModel(_modelPath);
        }

        public static void LoadModel(string modelPath) {
            _session?.Dispose();
            var options = new SessionOptions();
            // options.AppendExecutionProvider_CUDA(); 
            _session = new InferenceSession(modelPath, options);
            Debug.WriteLine($"Model version: {_session.ModelMetadata.Version}");
        }

        public void TransferStyle(string contentImagePath, string styleImagePath, string outputImagePath, float alpha = 1.0f, int contentSize = 512, int styleSize = 512) {
            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");

            // 预处理：加载并缩放图像 (注意使用 using 释放底层非托管内存)
            using Mat contentImage = LoadAndResizeImage(contentImagePath, contentSize);
            using Mat styleImage = LoadAndResizeImage(styleImagePath, styleSize);

            // 转换为 Tensor
            DenseTensor<float> contentTensor = ImageToTensor(contentImage);
            DenseTensor<float> styleTensor = ImageToTensor(styleImage);

            // 准备 Alpha 控制变量 (Shape: [1])
            DenseTensor<float> alphaTensor = new DenseTensor<float>(new float[] { alpha }, new int[] { 1 });

            // 构造输入
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("content", contentTensor),
                NamedOnnxValue.CreateFromTensor("style", styleTensor),
                NamedOnnxValue.CreateFromTensor("alpha", alphaTensor)
            };

            // 执行推理
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

            // 获取输出结果
            var outputTensor = results.First().AsTensor<float>();

            // 后处理：将 Tensor 转换回 OpenCV Mat 并保存
            using Mat resultImage = TensorToImage(outputTensor);
            resultImage.ImWrite(outputImagePath);
        }

        #region OpenCV 图像预处理与后处理

        private Mat LoadAndResizeImage(string imagePath, int targetSize) {
            // 读取图像 (OpenCV 默认是 BGR)
            Mat image = Cv2.ImRead(imagePath, ImreadModes.Color);

            // 转换为 RGB (对齐 PyTorch 的颜色通道)
            Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

            // 等比缩放 (最短边缩放至 targetSize)
            if (targetSize > 0) {
                int w = image.Width;
                int h = image.Height;
                int newW, newH;

                if (w < h) {
                    newW = targetSize;
                    newH = (int)(h * ((float)targetSize / w));
                }
                else {
                    newH = targetSize;
                    newW = (int)(w * ((float)targetSize / h));
                }

                Mat resizedImage = new Mat();
                // 使用双线性插值
                Cv2.Resize(image, resizedImage, new Size(newW, newH), 0, 0, InterpolationFlags.Linear);
                image.Dispose(); // 释放原图内存
                return resizedImage;
            }

            return image;
        }

        private DenseTensor<float> ImageToTensor(Mat img) {
            var tensor = new DenseTensor<float>(new[] { 1, 3, img.Height, img.Width });

            // 使用 Indexer 高效遍历 OpenCV 矩阵
            var indexer = img.GetGenericIndexer<Vec3b>();

            for (int y = 0; y < img.Height; y++) {
                for (int x = 0; x < img.Width; x++) {
                    Vec3b color = indexer[y, x];
                    // 因为前面转过 RGB，所以 Item0=R, Item1=G, Item2=B
                    tensor[0, 0, y, x] = color.Item0 / 255.0f; // R
                    tensor[0, 1, y, x] = color.Item1 / 255.0f; // G
                    tensor[0, 2, y, x] = color.Item2 / 255.0f; // B
                }
            }

            return tensor;
        }

        private Mat TensorToImage(Tensor<float> tensor) {
            int height = tensor.Dimensions[2];
            int width = tensor.Dimensions[3];

            // 创建一个 8位 3通道 的 OpenCV 矩阵
            Mat image = new Mat(height, width, MatType.CV_8UC3);
            var indexer = image.GetGenericIndexer<Vec3b>();

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    // 裁剪并放大到 0~255
                    float r = Math.Clamp(tensor[0, 0, y, x], 0.0f, 1.0f);
                    float g = Math.Clamp(tensor[0, 1, y, x], 0.0f, 1.0f);
                    float b = Math.Clamp(tensor[0, 2, y, x], 0.0f, 1.0f);

                    // OpenCV 在保存图片时，默认认为内存里的是 BGR 数据。
                    // 所以我们写入的时候，强行按照 B, G, R 的顺序装载 (Item0=B, Item1=G, Item2=R)
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
