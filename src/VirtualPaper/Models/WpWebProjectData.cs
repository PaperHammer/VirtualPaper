using System.Text.Json.Serialization;

namespace VirtualPaper.Models {
    [JsonSerializable(typeof(WpWebProjectData))]
    public partial class WpWebProjectDataContext : JsonSerializerContext { }

    /// <summary>
    /// zip 包内 project.json 的反序列化模型（VP 标准 web 壁纸）
    /// </summary>
    public class WpWebProjectData {
        /// <summary>壁纸标题</summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>壁纸描述</summary>
        [JsonPropertyName("description")]
        public string Desc { get; set; } = string.Empty;

        /// <summary>作者</summary>
        [JsonPropertyName("authors")]
        public string Authors { get; set; } = string.Empty;

        /// <summary>标签，分号分隔</summary>
        [JsonPropertyName("tags")]
        public string Tags { get; set; } = string.Empty;

        /// <summary>HTML 入口文件（相对路径），默认 index.html</summary>
        [JsonPropertyName("file")]
        public string File { get; set; } = "index.html";

        /// <summary>预览图文件（相对路径），默认 preview.jpg</summary>
        [JsonPropertyName("preview")]
        public string Preview { get; set; } = "preview.jpg";
    }
}
