using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace VirtualPaper.DraftPanel.Utils {
    internal partial class LayerCanvas : Canvas {
        public void AddLayer(int layerIndex) {
            if (!_layers.ContainsKey(layerIndex)) {
                var grid = new Grid();
                _layers[layerIndex] = grid;
                this.Children.Add(grid);
                // 保持图层按LayerIndex顺序排列
                ReorderLayers();
            }
        }

        public void RemoveLayer(int layerIndex) {
            if (_layers.TryGetValue(layerIndex, out Grid value)) {
                this.Children.Remove(value);
                _layers.Remove(layerIndex);
            }
        }

        private void ReorderLayers() {
            // 清除所有子元素并根据LayerIndex重新添加以确保正确的堆叠顺序
            this.Children.Clear();
            foreach (var layer in _layers.OrderBy(l => l.Key)) {
                this.Children.Add(layer.Value);
            }
        }

        public void ClearLayers() {
            this.Children.Clear();
            _layers.Clear();
        }

        internal readonly Dictionary<int, Grid> _layers = [];
    }
}
