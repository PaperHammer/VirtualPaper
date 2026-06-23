using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace VirtualPaper.UIComponent.Input {
    /// <summary>
    /// 扩展 Slider，提供三项自定义轨道能力：
    ///   <list type="bullet">
    ///     <item><see cref="TrackFill"/> — 自定义轨道填充画刷（渐变/纯色皆可）</item>
    ///     <item><see cref="TrackFillMode"/> — Progress：仅填充已播放部分；Full：全宽平铺恒定显示</item>
    ///     <item><see cref="KeepTrackColorOnHover"/> — 悬浮/点击时是否保持自定义颜色（Progress 模式专用）</item>
    ///   </list>
    /// </summary>
    public partial class ArcSlider : Slider {
        /// <summary>自定义轨道填充画刷，null 时使用系统主题色</summary>
        public Brush? TrackFill {
            get => (Brush?)GetValue(TrackFillProperty);
            set => SetValue(TrackFillProperty, value);
        }
        public static readonly DependencyProperty TrackFillProperty =
            DependencyProperty.Register(
                nameof(TrackFill), typeof(Brush), typeof(ArcSlider),
                new PropertyMetadata(null, (d, _) => ((ArcSlider)d).ApplyTrackStyle()));


        /// <summary>轨道填充显示模式</summary>
        public ArcSliderTrackFillMode TrackFillMode {
            get => (ArcSliderTrackFillMode)GetValue(TrackFillModeProperty);
            set => SetValue(TrackFillModeProperty, value);
        }
        public static readonly DependencyProperty TrackFillModeProperty =
            DependencyProperty.Register(
                nameof(TrackFillMode), typeof(ArcSliderTrackFillMode), typeof(ArcSlider),
                new PropertyMetadata(ArcSliderTrackFillMode.Progress, (d, _) => ((ArcSlider)d).ApplyTrackStyle()));

        /// <summary>
        /// 悬浮 / 拖动时是否保持 <see cref="TrackFill"/> 颜色不被主题色覆盖
        /// 仅在 <see cref="TrackFillMode"/> = <see cref="ArcSliderTrackFillMode.Progress"/> 且
        /// <see cref="TrackFill"/> 非 null 时生效；Full 模式下轨道永远稳定，此属性无需设置
        /// </summary>
        public bool KeepTrackColorOnHover {
            get => (bool)GetValue(KeepTrackColorOnHoverProperty);
            set => SetValue(KeepTrackColorOnHoverProperty, value);
        }
        public static readonly DependencyProperty KeepTrackColorOnHoverProperty =
            DependencyProperty.Register(
                nameof(KeepTrackColorOnHover), typeof(bool), typeof(ArcSlider),
                new PropertyMetadata(false));

        protected override void OnApplyTemplate() {
            base.OnApplyTemplate();
            _hDecreaseRect = GetTemplateChild("HorizontalDecreaseRect") as Rectangle;
            _vDecreaseRect = GetTemplateChild("VerticalDecreaseRect") as Rectangle;
            _hFullTrackFill = GetTemplateChild("HorizontalFullTrackFill") as Rectangle;
            _vFullTrackFill = GetTemplateChild("VerticalFullTrackFill") as Rectangle;
            ApplyTrackStyle();
        }

        private void ApplyTrackStyle() {
            if (TrackFill is null) return;   // 无自定义色，保持默认外观

            if (TrackFillMode == ArcSliderTrackFillMode.Full) {
                // 全宽平铺：显示全宽背景条，隐藏 DecreaseRect（用 Opacity 而非 Visibility，
                // 避免与 VisualState 的 Fill 动画冲突）
                if (_hFullTrackFill != null) {
                    _hFullTrackFill.Visibility = Visibility.Visible;
                    _hFullTrackFill.Fill = TrackFill;
                }
                if (_vFullTrackFill != null) {
                    _vFullTrackFill.Visibility = Visibility.Visible;
                    _vFullTrackFill.Fill = TrackFill;
                }
                if (_hDecreaseRect != null) _hDecreaseRect.Opacity = 0;
                if (_vDecreaseRect != null) _vDecreaseRect.Opacity = 0;
            }
            else {
                // Progress 模式：用自定义色替换 DecreaseRect 颜色
                if (_hDecreaseRect != null) {
                    _hDecreaseRect.Fill = TrackFill;
                    _hDecreaseRect.Opacity = 1;
                }
                if (_vDecreaseRect != null) {
                    _vDecreaseRect.Fill = TrackFill;
                    _vDecreaseRect.Opacity = 1;
                }
                if (_hFullTrackFill != null) _hFullTrackFill.Visibility = Visibility.Collapsed;
                if (_vFullTrackFill != null) _vFullTrackFill.Visibility = Visibility.Collapsed;
            }
        }

        // 悬浮 / 拖动时恢复自定义颜色（Progress 模式 + KeepTrackColorOnHover）
        private void RestoreProgressColor() {
            if (!KeepTrackColorOnHover
                || TrackFill is null
                || TrackFillMode != ArcSliderTrackFillMode.Progress) return;

            if (_hDecreaseRect != null) _hDecreaseRect.Fill = TrackFill;
            if (_vDecreaseRect != null) _vDecreaseRect.Fill = TrackFill;
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e) { base.OnPointerEntered(e); RestoreProgressColor(); }
        protected override void OnPointerExited(PointerRoutedEventArgs e) { base.OnPointerExited(e); RestoreProgressColor(); }
        protected override void OnPointerPressed(PointerRoutedEventArgs e) { base.OnPointerPressed(e); RestoreProgressColor(); }
        protected override void OnPointerReleased(PointerRoutedEventArgs e) { base.OnPointerReleased(e); RestoreProgressColor(); }
        protected override void OnPointerCanceled(PointerRoutedEventArgs e) { base.OnPointerCanceled(e); RestoreProgressColor(); }

        private Rectangle? _hDecreaseRect;
        private Rectangle? _vDecreaseRect;
        private Rectangle? _hFullTrackFill;
        private Rectangle? _vFullTrackFill;
    }
}
