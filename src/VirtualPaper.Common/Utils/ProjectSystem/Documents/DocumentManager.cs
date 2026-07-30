namespace VirtualPaper.Common.Utils.ProjectSystem.Documents {
    public class DocumentManager {
        private readonly Dictionary<string, Document> documents = [];

        public Document Open(string path) {
            if (documents.TryGetValue(path, out var doc))
                return doc;

            doc = new Document(path);
            documents[path] = doc;

            return doc;
        }

        public void Close(string path) {
            documents.Remove(path);
        }

        public bool IsOpen(string path) {
            return documents.ContainsKey(path);
        }

        public Document? Get(string path) {
            documents.TryGetValue(path, out var doc);
            return doc;
        }

        public void Rename(string oldPath, string newPath) {
            if (!documents.TryGetValue(oldPath, out var doc))
                return;

            documents.Remove(oldPath);
            doc.Rename(newPath);
            documents[newPath] = doc;
        }
    }
}
