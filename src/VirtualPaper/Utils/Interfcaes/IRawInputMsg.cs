using VirtualPaper.Common;
using VirtualPaper.Views.WindowsMsg;

namespace VirtualPaper.Utils.Interfcaes {
    public interface IRawInputMsg {
        InputForwardMode InputMode { get; }
        event EventHandler<MouseRawArgs>? MouseMoveRaw;
        event EventHandler<MouseClickRawArgs>? MouseDownRaw;
        event EventHandler<MouseClickRawArgs>? MouseUpRaw;
        event EventHandler<KeyboardClickRawArgs>? KeyboardClickRaw;
    }
}
