using System.Collections.Generic;
using Workloads.Creation.StaticImg.Models.ToolItemUtil;

namespace Workloads.Creation.StaticImg.Utils {
    class ToolManager {
        public void RegisterTool(ToolType toolType, Tool tool) {
            _tools[toolType] = tool;
        }

        public Tool GetTool(ToolType toolType) {
            return _tools.TryGetValue(toolType, out var tool) ? tool : null;
        }

        private readonly Dictionary<ToolType, Tool> _tools = new();
    }
}
