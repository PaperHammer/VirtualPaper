using System;

namespace Workloads.Creation.WebBackdrop.Models {
    public sealed class WebFileMovedEventArgs(string oldPath, string newPath) : EventArgs {
        public string OldPath { get; } = oldPath;
        public string NewPath { get; } = newPath;
    }
}
