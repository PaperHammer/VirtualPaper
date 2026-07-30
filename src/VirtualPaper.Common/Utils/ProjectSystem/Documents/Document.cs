namespace VirtualPaper.Common.Utils.ProjectSystem.Documents {
    public class Document {
        public string Path { get; private set; }

        // 编辑器中的内容
        public string Text { get; set; }

        // 是否有未保存修改
        public bool IsDirty { get; set; }

        // 当前内存对应的磁盘版本
        public FileStamp DiskStamp { get; private set; }

        public Document(string path) {
            Path = path;
            Text = ReadAllText(path);
            DiskStamp = FileStamp.Create(path);
        }

        /// <summary>
        /// 判断磁盘文件是否发生变化
        /// </summary>
        public bool IsDiskChanged() {
            return DiskStamp.IsChanged(Path);
        }

        /// <summary>
        /// 从磁盘重新加载
        /// </summary>
        public void ReloadFromDisk() {
            Text = ReadAllText(Path);
            DiskStamp = FileStamp.Create(Path);
            IsDirty = false;
        }

        /// <summary>
        /// 保存到磁盘
        /// </summary>
        public void Save() {
            File.WriteAllText(Path, Text);
            DiskStamp = FileStamp.Create(Path);
            IsDirty = false;
        }

        /// <summary>
        /// 外部写入后刷新磁盘指纹
        /// 
        /// 例如:
        /// 编辑器用自定义编码保存文件后调用此方法
        /// 避免 FileSystemWatcher 误判为外部修改
        /// </summary>
        public void RefreshDiskStamp() {
            DiskStamp = FileStamp.Create(Path);
            IsDirty = false;
        }

        public void Rename(string newPath) {
            Path = newPath;
            DiskStamp = FileStamp.Create(newPath);
        }

        private static string ReadAllText(string path) {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }
    }
}
