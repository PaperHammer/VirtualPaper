using System.Collections.Generic;
using Workloads.Creation.StaticImg.Models.ToolItemUtil;

namespace Workloads.Creation.StaticImg.Utils {
    class ToolManager {
        internal void RegisterTool(ToolType toolType, Tool tool) {
            _tools[toolType] = tool;
        }

        internal Tool GetTool(ToolType toolType) {
            return _tools.TryGetValue(toolType, out var tool) ? tool : null;
        }

        internal IEnumerable<Tool> GetAllTools() {
            return _tools.Values;
        }

        private readonly Dictionary<ToolType, Tool> _tools = [];
    }
}
