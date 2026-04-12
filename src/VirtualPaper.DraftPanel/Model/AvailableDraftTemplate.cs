using System.Collections.Generic;
using System.Text.Json.Serialization;
using VirtualPaper.Models.DraftPanel;

namespace VirtualPaper.DraftPanel.Model {
    [JsonSerializable(typeof(AvailableDraftTemplate))]
    [JsonSerializable(typeof(List<ProjectTemplate>))]
    public partial class AvailableDraftTemplateContext : JsonSerializerContext { }

    public class AvailableDraftTemplate {
        public string? DefaultProjectName { get; set; }
        public List<ProjectTemplate>? Templates { get; set; }
    }
}
