using MessagePack;

namespace VirtualPaper.Common.Utils.Storage {
    public static class MessagePackStorage {
        public static async Task SaveAsync<T>(string filePath, T obj) {
            if (!File.Exists(filePath)) {
                File.Create(filePath).Close();
            }

            byte[] bytes = MessagePackSerializer.Serialize(obj);
            await File.WriteAllBytesAsync(filePath, bytes);
        }

        public static async Task<T> LoadAsync<T>(string filePath) where T : new() {
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            byte[] bytes = await File.ReadAllBytesAsync(filePath);
            return MessagePackSerializer.Deserialize<T>(bytes);
        }
    }
}
