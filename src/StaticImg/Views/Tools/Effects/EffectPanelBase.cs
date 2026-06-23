using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Shader.Models;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    /// <summary>效果参数面板基类</summary>
    public abstract class EffectPanelBase : UserControl {
        public event EventHandler<EffectParams>? ParamsChanged;

        public abstract EffectParams Params { get; }

        /// <summary>
        /// true 表示此效果无需参数、点击即生效（如灰度、反相）
        /// ShowEffectPanel 检测到此标志后会立即调用一次 UpdateParams
        /// </summary>
        public virtual bool IsOneShot => false;

        /// <summary>重置面板参数到初始值（每次打开效果时调用）</summary>
        public virtual void Reset() { }

        protected void RaiseParamsChanged() => ParamsChanged?.Invoke(this, Params);

        protected static LinearGradientBrush Gradient(params (double offset, Windows.UI.Color color)[] stops) {
            var brush = new LinearGradientBrush {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
            };
            foreach (var (offset, color) in stops)
                brush.GradientStops.Add(new GradientStop { Offset = offset, Color = color });
            return brush;
        }
    }
}
