using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    public partial class Layer : IDisposable {
        const int LAYER_MAGIC = 0x4C415952; // "LAYR"的ASCII十六进制表示

        public string Name { get; init; } = string.Empty;
        public bool IsVisible { get; init; } = true;
        public InkRenderData RenderData { get; }

        public Layer(string name, bool isEnable, InkRenderData renderData) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsVisible = isEnable;
            RenderData = renderData ?? throw new ArgumentNullException(nameof(renderData));
        }

        private static async Task<byte[]> SerializeSignleLayerAsync(Layer layer) {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(layer.IsVisible);

            var nameBytes = Encoding.UTF8.GetBytes(layer.Name);
            writer.Write((ushort)nameBytes.Length);
            writer.Write(nameBytes);

            await layer.RenderData.SaveAsync(ms);
            return ms.ToArray();
        }

        public static async Task<byte[]> SerializeAsync(List<Layer> layers) {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            foreach (var layer in layers) {
                // 写入图层标识符（4字节魔数）
                writer.Write(LAYER_MAGIC);

                var layerBytes = await SerializeSignleLayerAsync(layer);

                writer.Write((int)layerBytes.Length);
                writer.Write(layerBytes);
            }

            return ms.ToArray();
        }

        private static async Task<Layer> DeserializeSignleAsync(byte[] data, ArcSize canvasSize) {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var renderData = new InkRenderData(canvasSize);
            var isEnable = reader.ReadBoolean();
            var name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));            
            var layer = new Layer(name, isEnable, renderData);

            ms.Position = 0; // Reset for InkRenderData
            await renderData.LoadAsync(ms);

            return layer;
        }

        public static async Task<List<Layer>> DeserializeAsync(FileStream fs, int layerCount, ArcSize canvasSize) {            
            using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

            var layers = new List<Layer>(layerCount);
            for (int i = 0; i < layerCount; i++) {
                long layerStartPos = fs.Position;

                try {
                    // 检查图层标识符
                    int magic = reader.ReadInt32();
                    if (magic != LAYER_MAGIC)
                        throw new InvalidDataException($"Invalid layer identifier (Position: {layerStartPos})");

                    // 读取数据长度
                    int layerSize = reader.ReadInt32();
                    if (layerSize <= 0 || layerSize > 100 * 1024 * 1024)
                        throw new InvalidDataException($"无效的图层数据大小: {layerSize} bytes");

                    // 读取数据
                    byte[] layerData = reader.ReadBytes(layerSize);
                    if (layerData.Length != layerSize)
                        throw new EndOfStreamException("Layer data is incomplete");

                    var layer = await DeserializeSignleAsync(layerData, canvasSize);
                    layers.Add(layer);
                }
                catch (Exception ex) when (i < layerCount - 1) {                    
                    ArcLog.GetLogger<StaticImg.MainPage>().Error($"Layer {i} deserialization failed: {ex.Message}");

                    // 尝试恢复位置到下一个图层起始处
                    if (!TryFindNextLayer(fs, LAYER_MAGIC)) {
                        ArcLog.GetLogger<StaticImg.MainPage>().Error("Unable to locate the next valid layer, aborting read.");
                        break;
                    }
                }
            }

            return layers;
        }

        /// <summary>
        /// 尝试定位下一个图层起始位置
        /// </summary>
        private static bool TryFindNextLayer(FileStream fs, int magicNumber) {
            long startPos = fs.Position;
            byte[] buffer = new byte[4096];
            byte[] magicBytes = BitConverter.GetBytes(magicNumber);

            while (fs.Position < fs.Length) {
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < bytesRead - 3; i++) {
                    if (buffer[i] == magicBytes[0] &&
                        buffer[i + 1] == magicBytes[1] &&
                        buffer[i + 2] == magicBytes[2] &&
                        buffer[i + 3] == magicBytes[3]) {
                        fs.Position = startPos + i;
                        return true;
                    }
                }
                startPos += bytesRead;
            }
            return false;
        }

        public void Dispose() {
            RenderData?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
