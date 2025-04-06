using System;
using Microsoft.UI.Xaml.Shapes;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    public class PathEventArgs(Path paintPath, OperationType operation) : EventArgs {
        public Path PaintPath { get; } = paintPath;
        public OperationType Operation { get; } = operation;
    }

    public enum OperationType {
        Add,
        Remove
    }
}
