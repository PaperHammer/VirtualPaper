using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using VirtualPaper.Common.Utils.Files;

namespace Workloads.Creation.StaticImg.Models.SerializableData {
    // Core part of StaticImgDesignFileUtil
    public partial class StaticImgDesignFileUtil {
        public string FilePath { get; private set; }
        public bool IsValidFile => File.Exists(FilePath);
        //public bool IsFileName { get; private set; }
        public bool HasDiff { get; private set; }

        public BusinessData? BusinessDataCache => _businessDataCache;

        private StaticImgDesignFileUtil(string path, bool isFileName) {
            FilePath = Path.GetFullPath(path);
            //IsFileName = isFileName;
            // 如果是 FileName 说明是新建文件，并未存储，默认和本地有 diff
            HasDiff = isFileName;
        }

        /// <summary>
        /// 创建ProjectFile 自动区分路径和文件名
        /// </summary>
        public static StaticImgDesignFileUtil Create(string input) {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Input cannot be empty");

            if (FileUtil.IsValidFilePath(input)) {
                return new StaticImgDesignFileUtil(input, false);
            }

            if (FileUtil.IsValidFileName(input)) {                
                return new StaticImgDesignFileUtil(Path.Combine(FileUtil.GetDocumentsDir(), input), true);
            }

            throw new ArgumentException("Input is neither a valid path nor filename");
        }

        private static byte[] StructureToBytes<T>(T structure) where T : struct {
            int size = Marshal.SizeOf<T>();
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(structure, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        private static T BytesToStructure<T>(byte[] bytes) where T : struct {
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            T structure = Marshal.PtrToStructure<T>(ptr);
            Marshal.FreeHGlobal(ptr);
            return structure;
        }

        /// <summary>
        /// 创建优化的文件流（带自动错误恢复）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="mode">文件模式</param>
        /// <param name="maxRetries">最大重试次数（默认3次）</param>
        /// <param name="bufferSize">缓冲区大小（默认8KB）</param>
        /// <returns>配置好的文件流</returns>
        private static FileStream CreateFileStream(
            string path,
            FileMode mode,
            int maxRetries = 3,
            int bufferSize = 8192) {
            FileStream? fs = null;
            Exception? lastError = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    // 创建目录（如需）
                    if (mode is FileMode.Create or FileMode.CreateNew or FileMode.OpenOrCreate) {
                        string? dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                    }

                    fs = new FileStream(
                        path,
                        mode,
                        GetFileAccess(mode),
                        FileShare.None,
                        bufferSize,
                        GetFileOptions(mode));

                    // 验证文件头（仅读取模式）
                    if (mode == FileMode.Open) {
                        var headerBytes = new byte[Marshal.SizeOf<FileHeader>()];
                        fs.Read(headerBytes);
                        var header = BytesToStructure<FileHeader>(headerBytes);

                        if (!header.IsValid())
                            throw new InvalidDataException("Invalid file header");

                        fs.Position = 0; // 重置位置
                    }

                    return fs;
                }
                catch (Exception ex) when (attempt < maxRetries) {
                    lastError = ex;
                    Thread.Sleep(100 * attempt); // 指数退避
                    fs?.Dispose();
                }
            }

            throw new IOException($"File stream creation failed (retry {maxRetries} times)", lastError);
        }

        private static FileAccess GetFileAccess(FileMode mode) => mode switch {
            FileMode.Open => FileAccess.Read,
            FileMode.Append => FileAccess.Write,
            _ => FileAccess.ReadWrite
        };

        private static FileOptions GetFileOptions(FileMode mode) =>
            FileOptions.Asynchronous |
            FileOptions.SequentialScan |
            (mode == FileMode.Create ? FileOptions.WriteThrough : FileOptions.None);

        /// <summary>
        /// 创建内存映射文件（适合大文件随机访问）
        /// </summary>
        private static (MemoryMappedFile mmf, MemoryMappedViewAccessor accessor) CreateMemoryMappedFile(
            string path,
            FileMode mode,
            long? capacity = null) {
            try {
                var mmf = MemoryMappedFile.CreateFromFile(
                    path,
                    mode,
                    null,
                    capacity ?? 1024 * 1024 * 100, // 默认100MB
                    MemoryMappedFileAccess.ReadWrite);

                return (mmf, mmf.CreateViewAccessor());
            }
            catch (IOException ex) {
                throw new InvalidOperationException("Memory-mapped file creation failed", ex);
            }
        }

        private void UpdateFile(string path) {
            File.Replace(path, FilePath, null, true);
        }

        private void UpdateCache(FileHeader? header = null, BusinessData? businessData = null, List<Layer>? layers = null) {
            if (header != null)
                _headerCache = header.Value;
            if (businessData != null)
                _businessDataCache = businessData;
            if (layers != null)
                _layersCache = layers;
        }

        private FileHeader _headerCache;
        private BusinessData? _businessDataCache;
        private List<Layer>? _layersCache;
        private readonly SemaphoreSlim _ioLock = new(1, 1);
    }
}
