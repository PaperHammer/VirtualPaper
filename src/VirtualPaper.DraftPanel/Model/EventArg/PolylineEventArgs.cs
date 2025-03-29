using System;
using Microsoft.UI.Xaml.Shapes;

namespace VirtualPaper.DraftPanel.Model.EventArg {
    public class PolylineEventArgs(Polyline polyline, OperationType operation) : EventArgs {
        public Polyline Polyline { get; } = polyline;
        public OperationType Operation { get; } = operation;
    }

    public enum OperationType {
        Add,
        Remove
    }
}
