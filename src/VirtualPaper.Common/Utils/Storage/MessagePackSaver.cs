using MessagePack;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Common.Utils.Storage {
    public static class MessagePackSaver {
        public static async Task SaveAsync<T>(string filePath, T obj) {
            try {
                if (!File.Exists(filePath)) {
                    File.Create(filePath).Close();
                }

                byte[] bytes = MessagePackSerializer.Serialize(obj);
                await File.WriteAllBytesAsync(filePath, bytes);
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "写入", ex);
            }
        }

        public static async Task<T> LoadAsync<T>(string filePath) where T : new() {
            try {
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                return MessagePackSerializer.Deserialize<T>(bytes);
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "读取", ex);
            }
        }

        public static async Task SaveIncrementalAsync<T>(string filePath, IEnumerable<T> changes) where T : class {
            try {
                if (!File.Exists(filePath)) {
                    File.Create(filePath).Close();
                }

                // 读取现有数据
                byte[] existingBytes = await File.ReadAllBytesAsync(filePath);
                var existingData = existingBytes.Length == 0 ? [] : MessagePackSerializer.Deserialize<List<T>>(existingBytes);

                // 将新变化添加到现有数据中，并处理删除
                var updatedData = new List<T>();
                foreach (var existingItem in existingData) {
                    var change = changes.FirstOrDefault(item => IsSameItem(item, existingItem));
                    if (change != null) {
                        if (((dynamic)change).IsDeleted) {
                            // 如果变更项标记为删除，则跳过此项目
                            continue;
                        }
                    }
                    updatedData.Add(existingItem);
                }

                // 添加新的变更（不在现有数据中的）
                foreach (var change in changes) {
                    if (!existingData.Any(item => IsSameItem(item, change))) {
                        updatedData.Add(change);
                    }
                }

                byte[] updatedBytes = MessagePackSerializer.Serialize(updatedData);
                await File.WriteAllBytesAsync(filePath, updatedBytes);

                foreach (var change in changes) {
                    MarkAsSaved(change);
                }
            }
            catch (Exception ex) {
                throw new FileAccessException(filePath, "写入", ex);
            }
        }

        private static bool IsSameItem<T>(T item1, T item2) where T : class {
            return item1 == item2;
        }

        private static void MarkAsSaved<T>(T item) where T : class {
            ((dynamic)item).IsSaved = true;
        }
    }
}
