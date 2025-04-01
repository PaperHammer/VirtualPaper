using System;
using VirtualPaper.DraftPanel.Model.Runtime;

namespace VirtualPaper.DraftPanel.Model.EventArg {
    internal class LayerNameChangedEventArgs : EventArgs {
        public string NewName { get; set; }
        public CanvasLayerData LayerData { get; set; }

        public LayerNameChangedEventArgs(string newName, CanvasLayerData layerData) {
            NewName = newName;
            LayerData = layerData;
        }
    }
}
