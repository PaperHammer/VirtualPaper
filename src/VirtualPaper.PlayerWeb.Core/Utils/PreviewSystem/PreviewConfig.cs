using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem {
    public class PreviewConfig {
        public List<string> Extensions { get; set; } = [
            ".html",
            ".css",
            ".js",
            ".json",
            ".wgsl"
        ];

        public bool IsPreviewFile(string path) {
            var ext = Path.GetExtension(path);

            return Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }
    }
}
