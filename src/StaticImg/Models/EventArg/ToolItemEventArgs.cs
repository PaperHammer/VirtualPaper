using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    internal class ToolItemEventArgs : EventArgs {
        public ToolItemEventArgs(PointerRoutedEventArgs pointerRoutedArgs, PointerPoint pointerPoint) {
            CurrentPointerRoutedArgs = pointerRoutedArgs;
            CurrentPointerPoint = pointerPoint;
        }

        public PointerRoutedEventArgs CurrentPointerRoutedArgs { get;  }
        public PointerPoint CurrentPointerPoint { get;  }

    }
}
