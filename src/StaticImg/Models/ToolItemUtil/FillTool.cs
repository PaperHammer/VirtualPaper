using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    class FillTool : ITool {
        public FillTool(LayerManagerData managerData) {
            _managerData = managerData;
        }

        public void OnPointerEntered(ToolItemEventArgs e) { }

        public void OnPointerExited(ToolItemEventArgs e) { }

        public void OnPointerMoved(ToolItemEventArgs e) { }

        public void OnPointerPressed(ToolItemEventArgs e) {
            var point = e.CurrentPointerPoint.Position;
            int startX = (int)point.X;
            int startY = (int)point.Y;

            //Color fillColor = pointerPoint.Properties.IsRightButtonPressed ?
            //    _managerData.BackgroundColor : _managerData.ForegroundColor;
            //_managerData.SelectedLayerData.FloodFill(startX, startY, fillColor, _managerData.Tolerance);
        }

        public void OnPointerReleased(ToolItemEventArgs e) { }

        private readonly LayerManagerData _managerData;
    }
}
