using Microsoft.UI.Xaml.Input;
using Workloads.Creation.StaticImg.Models.EventArg;

namespace Workloads.Creation.StaticImg.Models.ToolItemUtil {
    interface ITool {
        void OnPointerEntered(ToolItemEventArgs e);
        void OnPointerPressed(ToolItemEventArgs e);
        void OnPointerMoved(ToolItemEventArgs e);
        void OnPointerReleased(ToolItemEventArgs e);
        void OnPointerExited(ToolItemEventArgs e);
    }
}
