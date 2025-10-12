using System.Runtime.InteropServices;
using System.Text;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileHeader {
        // 基础标识区 (6字节)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Magic; // "VPD"标识
        public ushort Version; // 当前版本：1

        // 画布参数区 (16字节)
        public float CanvasWidth; // 画布宽度（像素）
        public float CanvasHeight; // 画布高度（像素）
        public uint Dpi; // 分辨率（DPI）
        public int LayerCount; // 图层数量

        // 数据布局区 (16字节)
        public uint ContentOffset; // 业务数据起始偏移
        public uint ContentLength; // 业务数据长度
        public uint LayersOffset; // 图层数据起始偏移
        public uint LayersLength; // 图层数据长度

        // 校验区 (4字节)
        public uint CRC32; // 头部校验和（计算时排除本字段）

        // 保留区 (2字节)
        private ushort _reserved;

        /// <summary>
        /// 创建新文件头
        /// </summary>
        public static FileHeader Create(float width, float height, uint dpi, int layerCount,
            uint contentLength, uint layersLength) {
            var header = new FileHeader {
                Magic = Encoding.ASCII.GetBytes("VPD"),
                Version = 1,
                CanvasWidth = width,
                CanvasHeight = height,
                Dpi = dpi,
                LayerCount = layerCount,
                ContentOffset = (uint)Marshal.SizeOf<FileHeader>(),
                ContentLength = contentLength,
                LayersOffset = (uint)Marshal.SizeOf<FileHeader>() + contentLength,
                LayersLength = layersLength,
                CRC32 = 0,
                _reserved = 0
            };

            return header;
        }

        /// <summary>
        /// 验证文件头有效性
        /// </summary>
        public readonly bool IsValid() {
            return Encoding.ASCII.GetString(Magic) == "VPD" &&
                    Version is 1 &&
                    CanvasWidth > 0 &&
                    CanvasHeight > 0 &&
                    Dpi >= 72 &&
                    Dpi <= 1200 &&
                    LayerCount >= 0 &&
                    ContentOffset >= Marshal.SizeOf<FileHeader>() &&
                    LayersOffset >= ContentOffset + ContentLength;
        }

        /// <summary>
        /// 获取文件总大小
        /// </summary>
        public readonly long GetTotalFileSize() =>
            Marshal.SizeOf<FileHeader>() + ContentLength + LayersLength;

        /// <summary>
        /// 设置保留字段标志位
        /// </summary>
        public void SetReservedFlag(byte flag) {
            _reserved = (ushort)((_reserved & 0xFF00) | flag);
        }

        /// <summary>
        /// 获取保留字段标志位
        /// </summary>
        public readonly byte GetReservedFlag() {
            return (byte)(_reserved & 0x00FF);
        }
    }
}
