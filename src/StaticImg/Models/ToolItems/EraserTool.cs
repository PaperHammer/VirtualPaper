using Microsoft.Graphics.Canvas;
using Workloads.Creation.StaticImg.Models.ToolItems.Base;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
     sealed partial class EraserTool(InkCanvasConfigData data) : TrackMapping(data) {
        protected override void InitCanvasBlend() {
            _blend = CanvasBlend.Copy;
        }
    }
}
