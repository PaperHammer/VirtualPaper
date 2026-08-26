using System.Collections.Generic;
using System.Text.Json.Serialization;
using VirtualPaper.Models.EditPanel;

namespace VirtualPaper.EditPanel.Model {
    [JsonSerializable(typeof(AvailableProjectTemplate))]
    [JsonSerializable(typeof(List<ProjectTemplate>))]
    public partial class AvailableDraftTemplateContext : JsonSerializerContext { }

    public class AvailableProjectTemplate {
        public string? DefaultProjectName { get; set; }
        public List<ProjectTemplate>? Templates { get; set; }
    }
}
