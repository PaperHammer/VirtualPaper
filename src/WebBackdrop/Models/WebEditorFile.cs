using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VirtualPaper.Models.Mvvm;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Models {
    public enum WebEditorFileKind {
        Text,
        Markdown,
        Image,
        Unsupported,
    }

    public partial class WebEditorFile : ObservableObject, IEquatable<WebEditorFile> {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public string FileExtension => Path.GetExtension(FilePath).ToLowerInvariant();

        public bool CanOpenAsText => Kind is WebEditorFileKind.Text or WebEditorFileKind.Markdown;
        public WebEditorFileKind Kind => GetKind(FileExtension);
        // [已废弃，暂注释] 全文同步改为保存时拉取，IndentText 无消费方
        // public string IndentText => GetIndentText(Content);
        public string EncodingText { get; private set; }
        public string LineEndingText { get; private set; }

        public void SetEncoding(string encoding) {
            if (EncodingText == encoding) return;
            EncodingText = encoding;
        }

        public async Task ReopenWithEncodingAsync(string encoding) {
            // 仅文本类文件重新读取内容；图片等非文本文件内容由文件路径驱动，不读二进制
            if (CanOpenAsText) {
                var enc = GetEncodingFromText(encoding);
                Content = await ReadAllTextAsync(FilePath, enc);
                LineEndingText = GetLineEndingText(_content);
            }

            _isSaved = true;
            IsSavedChanged?.Invoke(this, EventArgs.Empty);
        }

        private static Encoding GetEncodingFromText(string encoding) {
            return encoding switch {
                "UTF-8 BOM" => new UTF8Encoding(true),
                "UTF-16 LE" => Encoding.Unicode,
                "UTF-16 BE" => Encoding.BigEndianUnicode,
                _ => new UTF8Encoding(false),
            };
        }

        public void SetLineEnding(string lineEnding) {
            LineEndingText = lineEnding;
        }

        public string Content {
            get => _content;
            set {
                if (_content == value) return;
                _content = value;
                OnPropertyChanged();
                // IsSaved is managed by EditorStateChanged (Monaco's
                // postEditorState), not by Content changes.  Otherwise
                // undo back to saved state would first show "saved"
                // (from editorStateChanged) and then flip back to
                // "unsaved" (from here).
            }
        }

        public bool IsSaved {
            get => _isSaved;
            private set {
                if (_isSaved == value) return;
                _isSaved = value;
                OnPropertyChanged();
                IsSavedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 文件加载/重载失败时置为 true，阻止保存以避免覆盖可能可恢复的原始数据
        /// </summary>
        public bool IsLoadFailed { get; private set; }

        public void SetLoadFailed() {
            IsLoadFailed = true;
        }

        public event EventHandler? IsSavedChanged;

        // [已废弃，暂注释] 无调用方（打开文件均走 LoadAsync）
        /*
        public WebEditorFile(string filePath) {
            FilePath = filePath;
            _content = ReadContent(filePath);
            EncodingText = GetEncodingText(filePath);
            LineEndingText = GetLineEndingText(_content);
            _isSaved = true;
        }
        */

        public static async Task<WebEditorFile> LoadAsync(string filePath) {
            if (!File.Exists(filePath) || !WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))) {
                return new WebEditorFile(filePath, string.Empty, "UTF-8");
            }

            var (content, encodingText) = await ReadAllTextAndEncodingAsync(filePath);
            return new WebEditorFile(filePath, content, encodingText);
        }

        private WebEditorFile(string filePath, string content, string encodingText) {
            FilePath = filePath;
            _content = content;
            EncodingText = encodingText;
            LineEndingText = GetLineEndingText(content);
            _isSaved = true;
        }

        public void MarkAsSaved() {
            IsSaved = true;
        }

        public void SetSavedState(bool isSaved) {
            IsSaved = isSaved;
        }

        private static WebEditorFileKind GetKind(string extension) {
            if (WebEditorFileUtil.IsMarkdownExtension(extension)) return WebEditorFileKind.Markdown;
            if (WebEditorFileUtil.IsPreviewImageExtension(extension)) return WebEditorFileKind.Image;
            if (WebEditorFileUtil.IsTextExtension(extension)) return WebEditorFileKind.Text;
            return WebEditorFileKind.Unsupported;
        }

        // [已废弃，暂注释] 仅被已注释的公共构造函数使用
        /*
        private static string ReadContent(string filePath) {
            return File.Exists(filePath) && WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))
                ? ReadAllText(filePath)
                : string.Empty;
        }

        private static async Task<string> ReadContentAsync(string filePath) {
            return File.Exists(filePath) && WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))
                ? await ReadAllTextAsync(filePath)
                : string.Empty;
        }
        */

        // [已废弃，暂注释] 无消费方（缩进信息由 Monaco 的 editorStateChanged 提供）
        /*
        private static string GetIndentText(string content) {
            var tabCount = 0;
            var spaceCount = 0;
            var minSpaces = int.MaxValue;

            for (var index = 0; index < content.Length;) {
                var spaces = 0;
                var hasContent = false;

                while (index < content.Length && content[index] is not '\r' and not '\n') {
                    var character = content[index++];
                    if (!hasContent && character == '\t') {
                        tabCount++;
                        while (index < content.Length && content[index] is not '\r' and not '\n') index++;
                        break;
                    }
                    if (!hasContent && character == ' ') {
                        spaces++;
                    }
                    else if (!char.IsWhiteSpace(character)) {
                        hasContent = true;
                    }
                }

                if (hasContent && spaces > 0) {
                    spaceCount++;
                    minSpaces = Math.Min(minSpaces, spaces);
                }

                if (index < content.Length && content[index] == '\r') index++;
                if (index < content.Length && content[index] == '\n') index++;
            }

            if (tabCount > spaceCount) return "Tabs";
            return minSpaces == int.MaxValue ? "Spaces: 2" : $"Spaces: {Math.Clamp(minSpaces, 1, 8)}";
        }
        */

        private static string GetLineEndingText(string content) {
            var crlfCount = 0;
            var lfCount = 0;
            for (var i = 0; i < content.Length; i++) {
                if (content[i] != '\n') continue;
                if (i > 0 && content[i - 1] == '\r') crlfCount++;
                else lfCount++;
            }
            return crlfCount >= lfCount ? "CRLF" : "LF";
        }

        // [已废弃，暂注释] 仅被已注释的公共构造函数使用
        /*
        private static string GetEncodingText(string filePath) {
            if (!File.Exists(filePath) || !WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))) return "UTF-8";

            var bytes = ReadAllBytes(filePath);
            return GetEncodingText(bytes);
        }

        private static async Task<string> GetEncodingTextAsync(string filePath) {
            if (!File.Exists(filePath) || !WebEditorFileUtil.IsTextExtension(Path.GetExtension(filePath))) return "UTF-8";

            var bytes = await ReadAllBytesAsync(filePath);
            return GetEncodingText(bytes);
        }
        */

        // [已废弃，暂注释] 仅被已注释的方法使用
        /*
        private static string GetEncodingText(byte[] bytes) {
            return GetEncodingText(bytes.AsSpan());
        }
        */

        public override bool Equals(object? obj) {
            return Equals(obj as WebEditorFile);
        }

        public bool Equals(WebEditorFile? other) {
            return other != null && FilePath == other.FilePath;
        }

        public override int GetHashCode() {
            return FilePath.GetHashCode();
        }

        // [已废弃，暂注释] 仅被已注释的方法使用
        /*
        private static string ReadAllText(string path, Encoding? encoding = null) {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = encoding == null ? new StreamReader(fs) : new StreamReader(fs, encoding);
            return sr.ReadToEnd();
        }
        */

        private static async Task<(string Content, string EncodingText)> ReadAllTextAndEncodingAsync(string path) {
            return await RetryReadAsync(async () => {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
                var prefix = new byte[3];
                var prefixLength = await fs.ReadAsync(prefix);
                var encoding = GetEncodingFromText(GetEncodingText(prefix.AsSpan(0, prefixLength)));
                fs.Position = 0;
                using var sr = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true);
                return (await sr.ReadToEndAsync(), GetEncodingText(prefix.AsSpan(0, prefixLength)));
            });
        }

        private static string GetEncodingText(ReadOnlySpan<byte> bytes) {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return "UTF-8 BOM";
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return "UTF-16 LE";
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return "UTF-16 BE";
            return "UTF-8";
        }

        private static async Task<string> ReadAllTextAsync(string path, Encoding? encoding = null) {
            return await RetryReadAsync(async () => {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
                using var sr = encoding == null ? new StreamReader(fs) : new StreamReader(fs, encoding);
                return await sr.ReadToEndAsync();
            });
        }

        // [已废弃，暂注释] 仅被已注释的方法使用
        /*
        private static byte[] ReadAllBytes(string path) {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytes = new byte[fs.Length];
            fs.ReadExactly(bytes);
            return bytes;
        }

        private static async Task<byte[]> ReadAllBytesAsync(string path) {
            return await RetryReadAsync(async () => {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
                var bytes = new byte[fs.Length];
                await fs.ReadExactlyAsync(bytes);
                return bytes;
            });
        }
        */

        private static async Task<T> RetryReadAsync<T>(Func<Task<T>> read) {
            for (var attempt = 0; ; attempt++) {
                try {
                    return await read();
                }
                catch (IOException) when (attempt < 3) {
                    await Task.Delay(50 * (attempt + 1));
                }
                catch (UnauthorizedAccessException) when (attempt < 3) {
                    await Task.Delay(50 * (attempt + 1));
                }
            }
        }

        private string _content;
        private bool _isSaved;
    }
}
