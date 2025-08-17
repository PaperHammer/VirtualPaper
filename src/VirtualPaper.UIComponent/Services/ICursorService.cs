using System;
using Microsoft.UI.Input;

namespace VirtualPaper.UIComponent.Services {
    public interface ICursorService {
        void RequestCursorChange(InputCursor cursor);
        event EventHandler<CursorChangedEventArgs> SystemCursorChangeRequested;
    }

    public class CursorChangedEventArgs(InputCursor cursor) : EventArgs {
        public InputCursor Cursor { get; } = cursor;
    }
}
