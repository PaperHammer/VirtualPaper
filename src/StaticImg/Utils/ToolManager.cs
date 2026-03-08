using System.Collections.Generic;
using VirtualPaper.Common.Logging;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.ViewModels;

namespace Workloads.Creation.StaticImg.Utils {
    class ToolManager {
        public InkCanvasViewModel ViewModel { get; }
        
        public ToolManager(InkCanvasViewModel viewModel) {
            ViewModel = viewModel;
        }

        public void RegisterTool(ToolType toolType, RenderBase tool) {
            if (_tools.ContainsKey(toolType)) {
                ArcLog.GetLogger<ToolManager>().Warn($"Tool of type {toolType} is already registered. Skipping registration.");
                return;
            }

            tool.ViewModel = ViewModel;
            _tools[toolType] = tool;
        }

        public RenderBase? GetTool(ToolType toolType) {
            return _tools.TryGetValue(toolType, out var tool) ? tool : null;
        }

        public IEnumerable<RenderBase> GetAllTools() {
            return _tools.Values;
        }

        public void RefreshToolRenderData() {
            foreach (var tool in GetAllTools()) {
                tool.OnLayerChanged();
            }
        }

        private readonly Dictionary<ToolType, RenderBase> _tools = [];
    }
}
