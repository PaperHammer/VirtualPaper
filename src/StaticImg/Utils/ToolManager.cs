using System.Collections.Generic;
using BuiltIn.InkSystem.Tool;

namespace Workloads.Creation.StaticImg.Utils {
    class ToolManager {
        internal void RegisterTool(ToolType toolType, RenderBase tool) {
            _tools[toolType] = tool;
        }

        internal RenderBase? GetTool(ToolType toolType) {
            return _tools.TryGetValue(toolType, out var tool) ? tool : null;
        }

        internal IEnumerable<RenderBase> GetAllTools() {
            return _tools.Values;
        }

        private readonly Dictionary<ToolType, RenderBase> _tools = [];
    }
}
