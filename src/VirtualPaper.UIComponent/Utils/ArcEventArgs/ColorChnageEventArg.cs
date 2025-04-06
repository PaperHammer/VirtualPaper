using System;
using Windows.UI;

namespace VirtualPaper.UIComponent.Utils.ArcEventArgs {
    public class ColorChnageEventArgs : EventArgs {
        public Color? OldItem { get; set; }
        public Color? NewItem { get; set; }
    }
}
