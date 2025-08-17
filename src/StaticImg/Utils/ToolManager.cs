using System.Collections.Generic;
using BuiltIn.Tool.Bsae;

namespace Workloads.Creation.StaticImg.Utils {
    class ToolManager {
        internal void RegisterTool(ToolType toolType, CanvasRenderTargetInteract tool) {
            _tools[toolType] = tool;
        }

        internal CanvasRenderTargetInteract? GetTool(ToolType toolType) {
            return _tools.TryGetValue(toolType, out var tool) ? tool : null;
        }

        internal IEnumerable<CanvasRenderTargetInteract> GetAllTools() {
            return _tools.Values;
        }

        private readonly Dictionary<ToolType, CanvasRenderTargetInteract> _tools = [];
    }
}
