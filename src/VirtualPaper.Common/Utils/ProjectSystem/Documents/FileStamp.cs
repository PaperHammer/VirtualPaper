namespace VirtualPaper.Common.Utils.ProjectSystem.Documents {
    public readonly record struct FileStamp(DateTime LastWriteTimeUtc, long Length) {
        public static FileStamp Create(string path) {
            var info = new FileInfo(path);

            return new FileStamp(info.LastWriteTimeUtc, info.Length);
        }

        public bool IsChanged(string path) {
            return this != Create(path);
        }
    }
}
