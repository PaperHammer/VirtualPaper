using System;
using Windows.UI;

namespace VirtualPaper.UIComponent.Utils.ArcEventArgs {
    public class ColorChnageEventArgs : EventArgs {
        public Color? RemoveItem { get; set; }
        public Color? AddItem { get; set; }
    }
}
