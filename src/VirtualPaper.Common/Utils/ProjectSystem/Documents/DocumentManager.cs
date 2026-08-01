namespace VirtualPaper.Common.Utils.ProjectSystem.Documents {
    /// <summary>
    /// 文档管理器，负责管理项目中被打开的文档对象
    /// </summary>
    public class DocumentManager {
        public Document Open(string path) {
            if (_documents.TryGetValue(path, out var doc))
                return doc;

            doc = new Document(path);
            _documents[path] = doc;

            return doc;
        }

        public void Close(string path) {
            _documents.Remove(path);
        }

        public bool IsOpen(string path) {
            return _documents.ContainsKey(path);
        }

        public Document? Get(string path) {
            _documents.TryGetValue(path, out var doc);
            return doc;
        }

        public void Rename(string oldPath, string newPath) {
            if (!_documents.TryGetValue(oldPath, out var doc))
                return;

            _documents.Remove(oldPath);
            doc.Rename(newPath);
            _documents[newPath] = doc;
        }
        
        private readonly Dictionary<string, Document> _documents = [];
    }
}
