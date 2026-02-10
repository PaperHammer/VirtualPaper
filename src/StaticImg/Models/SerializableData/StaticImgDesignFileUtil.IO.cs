using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Security;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.StaticImg.Core.Utils;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    // IO part of StaticImgDesignFileUtil
    partial class StaticImgDesignFileUtil {
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
                using var fs = CreateFileStream(FilePath, FileMode.Open);

                // Read and validate header
                var headerBytes = new byte[Marshal.SizeOf<FileHeader>()];
                await fs.ReadAsync(headerBytes);
                var header = BytesToStructure<FileHeader>(headerBytes);

                if (!header.IsValid())
                    throw new InvalidDataException("Invalid file header");

                // Read business data
                fs.Position = header.ContentOffset;
                var businessDataBytes = new byte[header.ContentLength];
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
            float canvasWidth,
            float canvasHeight,
            uint dpi,
            BusinessData business,
            List<Layer> layers) {
            await _ioLock.WaitAsync();

            if (!IsValidFile) {
                string? selectedPath = (await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle))?.Path;
                if (string.IsNullOrEmpty(selectedPath))
                    return (false, null);

                FilePath = selectedPath;
            }

            try {
                // Serialize data
                var businessDataBytes = BusinessData.Serialize(business);
                var layersBytes = await Layer.SerializeAsync(layers);

                // Create header
                var header = FileHeader.Create(
                    canvasWidth,
                    canvasHeight,
                    dpi,
                    layers.Count,
                    (uint)businessDataBytes.Length,
                    (uint)layersBytes.Length);
                byte[] buffer = StructureToBytes(header);
                // 计算并回填CRC（跳过最后4字节的CRC字段）
                header.CRC32 = CrcUtils.ComputeCrc32(buffer.AsSpan(0, buffer.Length - 4));

                // Write to temp file
                string tempPath = Path.GetTempFileName();
                using var fs = CreateFileStream(tempPath, FileMode.Create);
                await fs.WriteAsync(StructureToBytes(header));
                await fs.WriteAsync(businessDataBytes);
                await fs.WriteAsync(layersBytes);

                UpdateFile(tempPath);
                UpdateCache(header, business, layers);

                return (true, FilePath);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);
                return (false, null);
            }
            finally {
                _ioLock.Release();
            }
        }

        // 单独保存 BusinessData
        public async Task<bool> SaveBusinessDataAsync(BusinessData business) {
            if (!IsValidFile) {
                GlobalMessageUtil.ShowError(
                    ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)),
                    message: Constants.I18n.Project_FileSave_Failed,
                    key: Constants.I18n.Project_FileSave_Failed,
                    isNeedLocalizer: true,
                    extraMsg: FilePath);
                return false;
            }

            await _ioLock.WaitAsync();

            try {
                var businessDataBytes = BusinessData.Serialize(business);

                string tempPath = Path.GetTempFileName();
                using var fs = CreateFileStream(tempPath, FileMode.OpenOrCreate);

                // Update header
                var header = _headerCache;
                header.ContentLength = (uint)businessDataBytes.Length;

                // Write updated header and business data
                await fs.WriteAsync(StructureToBytes(header));
                await fs.WriteAsync(businessDataBytes);

                // If layers exist, copy them over
                if (header.LayersLength > 0) {
                    fs.Position = header.LayersOffset;
                    using var sourceFs = CreateFileStream(FilePath, FileMode.Open);
                    sourceFs.Position = header.LayersOffset;
                    await sourceFs.CopyToAsync(fs);
                }

                UpdateFile(tempPath);
                UpdateCache(businessData: business);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);
                return false;
            }
            finally {
                _ioLock.Release();
            }
        }

        // 单独保存 Layers
        public async Task<bool> SaveLayersAsync(List<Layer> layers) {
            if (!IsValidFile) {
                GlobalMessageUtil.ShowError(
                    ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), 
                    message: Constants.I18n.Project_FileSave_Failed,
                    key: Constants.I18n.Project_FileSave_Failed,
                    isNeedLocalizer: true,
                    extraMsg: FilePath);
                return false;
            }

            await _ioLock.WaitAsync();

            try {
                var layersBytes = await Layer.SerializeAsync(layers);

                string tempPath = Path.GetTempFileName();
                using var fs = CreateFileStream(tempPath, FileMode.OpenOrCreate);

                // Update header
                var header = _headerCache;
                header.LayerCount = layers.Count;
                header.LayersLength = (uint)layersBytes.Length;
                header.LayersOffset = (uint)Marshal.SizeOf<FileHeader>() + header.ContentLength;

                // Write header and business data
                await fs.WriteAsync(StructureToBytes(header));
                if (header.ContentLength > 0) {
                    using var sourceFs = CreateFileStream(FilePath, FileMode.Open);
                    sourceFs.Position = Marshal.SizeOf<FileHeader>();
                    var businessDataBytes = new byte[header.ContentLength];
                    await sourceFs.ReadAsync(businessDataBytes);
                    await fs.WriteAsync(businessDataBytes);
                }

                // Write new layers
                await fs.WriteAsync(layersBytes);

                UpdateFile(tempPath);
                UpdateCache(layers: layers);

                return true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<StaticImgDesignFileUtil>().Error(ex);
                return false;
            }
            finally {
                _ioLock.Release();
            }
        }
    }
}
