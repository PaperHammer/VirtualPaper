using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Security;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Core.Utils;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    // IO part of StaticImgDesignFileUtil
    partial class StaticImgDesignFileUtil {
        // 定义缓冲区大小：1MB
        private const int COPY_BUFFER_SIZE = 1024 * 1024;

        /// <summary>
        /// 仅异步获取文件头信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>成功返回 Header，失败或无效返回 null</returns>
        public static async Task<FileHeader?> GetFileHeaderAsync(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return null;
            }

            try {
                // 计算 Header 结构体的大小
                int headerSize = Marshal.SizeOf<FileHeader>();
                byte[] headerBytes = new byte[headerSize];

                using var fs = CreateFileStream(filePath, FileMode.Open, autoCheckHeaderOnOpenMode: true);
                int bytesRead = await fs.ReadAsync(headerBytes.AsMemory(0, headerSize));

                if (bytesRead < headerSize) {
                    GlobalMessageUtil.ShowWarning(
                        ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                        message: nameof(Constants.I18n.Project_FileLoad_FileCorrupted),
                        isNeedLocalizer: true,
                        extraMsg: filePath);
                    return null;
                }

                var header = BytesToStructure<FileHeader>(headerBytes);

                if (!header.IsValid()) {
                    return null;
                }

                return header;
            }
            catch (Exception ex) {
                // 记录日志 (静态方法中通常没有实例 logger，需通过泛型获取)
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error($"GetFileHeaderAsync Failed: {filePath}", ex);
                return null;
            }
        }

        public async Task<(FileHeader? fileHeader, BusinessData? businessData, List<Layer>? layers)> LoadAsync(InkProjectSession session) {
            if (!IsValidFile) {
                GlobalMessageUtil.ShowError(
                    ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                    message: Constants.I18n.Project_FileLoad_Failed,
                    key: Constants.I18n.Project_FileLoad_Failed,
                    isNeedLocalizer: true,
                    extraMsg: FilePath);
                return (null, null, null);
            }

            await _ioLock.WaitAsync();

            try {
                using var fs = CreateFileStream(FilePath, FileMode.Open, autoCheckHeaderOnOpenMode: true);

                // Read and validate header
                var headerBytes = new byte[Marshal.SizeOf<FileHeader>()];
                await fs.ReadAsync(headerBytes);
                var header = BytesToStructure<FileHeader>(headerBytes);

                // Read business data
                fs.Position = header.BusinessDataOffset;
                var businessDataBytes = new byte[header.BusinessDataLength];
                await fs.ReadAsync(businessDataBytes);
                var businessData = BusinessData.Deserialize(businessDataBytes);

                // Read layers
                fs.Position = header.LayersOffset;
                var canvasSize = new ArcSize(header.CanvasWidth, header.CanvasHeight, header.Dpi, RebuildMode.None);
                var layers = await Layer.DeserializeAsync(session, fs, header.LayerCount, canvasSize);

                UpdateCache(header, businessData, layers);
                return (header, businessData, layers);

            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);
                return (null, null, null);
            }
            finally {
                _ioLock.Release();
            }
        }

        public async Task<(bool Success, string? FinalPath)> SaveAsync(
            ArcSize arcSize,
            BusinessData business,
            List<Layer> layers) {
            await _ioLock.WaitAsync();
            string tempPath = FileUtil.GetTempFile(Constants.CommonPaths.TempDir);

            try {
                var businessDataBytes = BusinessData.Serialize(business);
                var layersBytes = await Layer.SerializeAsync(layers);
                var header = FileHeader.Create(
                    arcSize,
                    layers.Count,
                    (uint)businessDataBytes.Length,
                    (uint)layersBytes.Length);

                byte[] buffer = StructureToBytes(header);
                // 计算并回填CRC（跳过最后4字节的CRC字段）
                header.CRC32 = CrcUtils.ComputeCrc32(buffer.AsSpan(0, buffer.Length - 4));
                buffer = StructureToBytes(header); // 更新包含 CRC 的 Buffer

                {
                    using var fs = CreateFileStream(tempPath, FileMode.Create);
                    await fs.WriteAsync(buffer);
                    await fs.WriteAsync(businessDataBytes);
                    await fs.WriteAsync(layersBytes);
                }

                UpdateFile(tempPath);
                UpdateCache(header, business, layers);

                return (true, FilePath);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);

                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) {
                    try { File.Delete(tempPath); } catch { /* 忽略删除临时文件时的二次错误 */ }
                }

                return (false, null);
            }
            finally {
                _ioLock.Release();
            }
        }

        // 单独保存 BusinessData
        public async Task<bool> SaveBusinessDataAsync(BusinessData business) {
            if (!IsValidFile) {
                return false;
            }

            await _ioLock.WaitAsync();
            string tempPath = FileUtil.GetTempFile(Constants.CommonPaths.TempDir);

            try {
                var businessDataBytes = BusinessData.Serialize(business);
                var header = _headerCache;
                header.BusinessDataLength = (uint)businessDataBytes.Length;
                header.LayersOffset = (uint)Marshal.SizeOf<FileHeader>() + header.BusinessDataLength;

                // 重新计算 Header CRC
                byte[] headerBuffer = StructureToBytes(header);
                header.CRC32 = CrcUtils.ComputeCrc32(headerBuffer.AsSpan(0, headerBuffer.Length - 4));
                headerBuffer = StructureToBytes(header);

                {
                    using var fsTemp = CreateFileStream(tempPath, FileMode.Create);

                    await fsTemp.WriteAsync(headerBuffer);
                    await fsTemp.WriteAsync(businessDataBytes);

                    // 如果原文件有 Layers，需要从原文件复制过来
                    if (header.LayersLength > 0) {
                        using var fsSource = CreateFileStream(FilePath, FileMode.Open);
                        // 定位到原文件中 Layers 的位置 (使用缓存中的旧 Offset 读取)
                        fsSource.Position = _headerCache.LayersOffset;
                        await CopyStreamRangeAsync(fsSource, fsTemp, _headerCache.LayersLength);

                        //// 复制 Layers 数据段到临时文件
                        //// 只复制 LayersLength 长度的数据，避免多余读取
                        //var buffer = new byte[header.LayersLength];
                        //await fsSource.ReadAsync(buffer);
                        //await fsTemp.WriteAsync(buffer);
                    }
                }

                UpdateFile(tempPath);
                UpdateCache(businessData: business, header: header);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);
                SafeDeleteTempFile(tempPath);
                return false;
            }
            finally {
                _ioLock.Release();
            }
        }

        // 单独保存 Layers
        public async Task<bool> SaveLayersAsync(List<Layer> layers) {
            if (!IsValidFile) {
                return false;
            }

            await _ioLock.WaitAsync();
            string tempPath = FileUtil.GetTempFile(Constants.CommonPaths.TempDir);

            try {
                var layersBytes = await Layer.SerializeAsync(layers);

                // Update header
                var header = _headerCache;
                header.LayerCount = layers.Count;
                header.LayersLength = (uint)layersBytes.Length;
                header.LayersOffset = (uint)Marshal.SizeOf<FileHeader>() + header.BusinessDataLength;

                byte[] headerBuffer = StructureToBytes(header);
                header.CRC32 = CrcUtils.ComputeCrc32(headerBuffer.AsSpan(0, headerBuffer.Length - 4));
                headerBuffer = StructureToBytes(header);

                {
                    using var fsTemp = CreateFileStream(tempPath, FileMode.Create);
                    await fsTemp.WriteAsync(StructureToBytes(header));

                    if (header.BusinessDataLength > 0) {
                        using var fsSource = CreateFileStream(FilePath, FileMode.Open);
                        fsSource.Position = _headerCache.BusinessDataOffset; // 使用缓存中的偏移量

                        // 使用流复制，避免内存问题
                        await CopyStreamRangeAsync(fsSource, fsTemp, _headerCache.BusinessDataLength);
                        //var businessDataBytes = new byte[header.ContentLength];
                        //await fsSource.ReadAsync(businessDataBytes);
                        //await fsTemp.WriteAsync(businessDataBytes);
                    }

                    await fsTemp.WriteAsync(layersBytes);
                }

                UpdateFile(tempPath);
                UpdateCache(layers: layers, header: header);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);
                SafeDeleteTempFile(tempPath);
                return false;
            }
            finally {
                _ioLock.Release();
            }
        }

        internal void SetFilePath(string selectedPath) {
            FilePath = selectedPath;
        }

        /// <summary>
        /// 仅复制流中指定长度的数据，避免读取整个剩余流或大量内存分配
        /// </summary>
        private static async Task CopyStreamRangeAsync(Stream input, Stream output, long bytesToCopy) {
            byte[] buffer = new byte[COPY_BUFFER_SIZE];
            long remaining = bytesToCopy;

            while (remaining > 0) {
                int count = (int)Math.Min(buffer.Length, remaining);
                int read = await input.ReadAsync(buffer.AsMemory(0, count));
                if (read == 0) break; // End of stream unexpected

                await output.WriteAsync(buffer.AsMemory(0, read));
                remaining -= read;
            }
        }

        private static void SafeDeleteTempFile(string? path) {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                try { File.Delete(path); } catch { /* Ignore */ }
            }
        }
    }
}
