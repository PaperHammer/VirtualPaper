using System;
using Microsoft.UI.Input;

namespace VirtualPaper.UIComponent.Services {
    public interface ICursorService {
        void RequestCursorChange(InputCursor cursor);
        event EventHandler<CursorChangedEventArgs> SystemCursorChangeRequested;
    }

    public class CursorChangedEventArgs : EventArgs {
        public InputCursor Cursor { get; }
        public CursorChangedEventArgs(InputCursor cursor) => Cursor = cursor;
    }
}
