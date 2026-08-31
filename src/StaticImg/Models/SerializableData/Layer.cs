using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;
using Workloads.Creation.StaticImg.Core.Utils;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    public partial class Layer : IDisposable {
        const int LAYER_MAGIC = 0x4C415952; // "LAYR"的ASCII十六进制表示

        public string Name { get; init; } = string.Empty;
        public LayerState State { get; set; }
        public InkRenderData RenderData { get; }

        public Layer(string name, LayerState state, InkRenderData renderData) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            State = state;
            RenderData = renderData ?? throw new ArgumentNullException(nameof(renderData));
        }

        private static async Task SerializeSingleLayerAsync(
            Layer layer,
            Stream output,
            ushort fileVersion,
            CancellationToken cancellationToken) {
            using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);

            layer.State.Serialize(writer);

            var nameBytes = Encoding.UTF8.GetBytes(layer.Name);
            writer.Write((ushort)nameBytes.Length);
            writer.Write(nameBytes);
            writer.Flush();

            if (fileVersion == FileHeader.Version1)
                await layer.RenderData.SaveAsync(output, ct: cancellationToken);
            else if (fileVersion == FileHeader.Version2)
                await layer.RenderData.SavePngAsync(output, cancellationToken);
            else
                throw new NotSupportedException($"不支持 VPD v{fileVersion} 图层序列化。");
        }

        /// <summary>
        /// 将图层逐个写入可寻址流。图层长度在该图层完成后回填，
        /// 避免为单图层和全部图层创建聚合字节数组。
        /// </summary>
        /// <returns>写入的全部图层数据长度。</returns>
        public static async Task<long> SerializeToStreamAsync(
            IReadOnlyList<Layer> layers,
            Stream output,
            ushort fileVersion = FileHeader.CurrentVersion,
            CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentNullException.ThrowIfNull(output);
            if (!output.CanWrite || !output.CanSeek)
                throw new ArgumentException("图层序列化需要可写且可寻址的流。", nameof(output));

            long layersStart = output.Position;

            foreach (var layer in layers) {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteLengthPrefixedBlockAsync(
                    output,
                    (target, token) => SerializeSingleLayerAsync(
                        layer,
                        target,
                        fileVersion,
                        token),
                    cancellationToken);
            }

            return output.Position - layersStart;
        }

        /// <summary>
        /// 写入图层块并在载荷完成后回填 32 位长度。
        /// v1、v2 共用该外层布局，区别仅在块内的渲染数据编码。
        /// </summary>
        internal static async Task<long> WriteLengthPrefixedBlockAsync(
            Stream output,
            Func<Stream, CancellationToken, Task> writePayloadAsync,
            CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(writePayloadAsync);
            if (!output.CanWrite || !output.CanSeek)
                throw new ArgumentException("图层序列化需要可写且可寻址的流。", nameof(output));

            long blockStart = output.Position;
            using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
            writer.Write(LAYER_MAGIC);

            // 图层长度只能在渲染数据写完后确定，先预留 4 字节。
            long layerLengthPosition = output.Position;
            writer.Write(0);
            writer.Flush();

            long layerStart = output.Position;
            await writePayloadAsync(output, cancellationToken);
            long layerEnd = output.Position;
            long layerLength = layerEnd - layerStart;
            if (layerLength > int.MaxValue)
                throw new InvalidDataException($"单图层数据超过格式上限: {layerLength} bytes。");

            // 回填长度后恢复到图层末尾，继续写下一层。
            output.Position = layerLengthPosition;
            writer.Write((int)layerLength);
            writer.Flush();
            output.Position = layerEnd;
            return layerEnd - blockStart;
        }

        private static async Task<Layer> DeserializeV1Async(InkProjectSession session, byte[] data, ArcSize canvasSize) {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var renderData = new InkRenderData(session, canvasSize);
            var state = LayerState.Deserialize(reader);
            ushort nameLength = reader.ReadUInt16();
            byte[] nameBytes = reader.ReadBytes(nameLength);
            if (nameBytes.Length != nameLength)
                throw new EndOfStreamException("Layer name data is incomplete.");
            var name = Encoding.UTF8.GetString(nameBytes);
            var layer = new Layer(name, state, renderData);

            await renderData.LoadAsync(ms);

            return layer;
        }

        private static async Task<Layer> DeserializeV2Async(
            InkProjectSession session,
            FileStream input,
            long layerEnd,
            ArcSize canvasSize) {
            using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
            var renderData = new InkRenderData(session, canvasSize);
            try {
                var state = LayerState.Deserialize(reader);
                ushort nameLength = reader.ReadUInt16();
                if (input.Position + nameLength > layerEnd)
                    throw new EndOfStreamException("Layer name data is incomplete.");

                byte[] nameBytes = reader.ReadBytes(nameLength);
                if (nameBytes.Length != nameLength)
                    throw new EndOfStreamException("Layer name data is incomplete.");

                string name = Encoding.UTF8.GetString(nameBytes);
                long pngLength = layerEnd - input.Position;
                if (pngLength <= 0) throw new InvalidDataException("PNG 图层数据为空。");

                using var pngSegment = new RelativeStream(
                    input,
                    input.Position,
                    pngLength,
                    leaveOpen: true);
                await renderData.LoadPngAsync(pngSegment);
                input.Position = layerEnd;
                return new Layer(name, state, renderData);
            }
            catch {
                renderData.Dispose();
                throw;
            }
        }

        public static async Task<List<Layer>> DeserializeAsync(
            InkProjectSession session,
            FileStream fs,
            int layerCount,
            ArcSize canvasSize,
            ushort fileVersion) {
            using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

            var layers = new List<Layer>(layerCount);
            for (int i = 0; i < layerCount; i++) {
                long layerStartPos = fs.Position;

                try {
                    // 检查图层标识符
                    int magic = reader.ReadInt32();
                    if (magic != LAYER_MAGIC)
                        throw new InvalidDataException($"Invalid layer identifier (Position: {layerStartPos})");

                    // 图层长度用于限制 v2 PNG 的随机访问范围，也用于校验 v1 载荷。
                    int layerSize = reader.ReadInt32();
                    long layerEnd = checked(fs.Position + layerSize);
                    if (layerSize <= 0 || layerEnd > fs.Length)
                        throw new InvalidDataException($"无效的图层数据大小: {layerSize} bytes");

                    Layer layer;
                    if (fileVersion == FileHeader.Version1) {
                        byte[] layerData = reader.ReadBytes(layerSize);
                        if (layerData.Length != layerSize)
                            throw new EndOfStreamException("Layer data is incomplete");
                        layer = await DeserializeV1Async(session, layerData, canvasSize);
                    }
                    else if (fileVersion == FileHeader.Version2) {
                        layer = await DeserializeV2Async(session, fs, layerEnd, canvasSize);
                    }
                    else {
                        throw new NotSupportedException($"不支持 VPD v{fileVersion} 图层反序列化。");
                    }
                    layers.Add(layer);
                }
                catch (Exception ex) when (i < layerCount - 1) {                    
                    ArcLog.GetLogger<MainPage>().Error($"Layer {i} deserialization failed: {ex.Message}");

                    // 尝试恢复位置到下一个图层起始处
                    if (!TryFindNextLayer(fs, LAYER_MAGIC)) {
                        ArcLog.GetLogger<MainPage>().Error("Unable to locate the next valid layer, aborting read.");
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
