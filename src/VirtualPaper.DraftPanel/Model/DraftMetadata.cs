using System;
using System.Collections.Generic;

namespace VirtualPaper.DraftPanel.Model {
    public class DraftMetadata {
        public string Name { get; set; } = string.Empty;
        public Version DraftVersion { get; set; }
        public string FolderPath{ get; set; } = string.Empty;
        public List<ProjectMetadata> DraftContains { get; set; } = [];
    }
}
