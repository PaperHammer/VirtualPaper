using System;
using Microsoft.UI.Xaml.Input;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    internal class CanvasPointerEventArgs  : EventArgs {
        public PointerRoutedEventArgs OriginalArgs { get; }
        public CanvasLayerResources CanvasResources { get; }

        public CanvasPointerEventArgs (PointerRoutedEventArgs args, CanvasLayerResources resources) {
            OriginalArgs = args;
            CanvasResources = resources;
        }
    }
}
