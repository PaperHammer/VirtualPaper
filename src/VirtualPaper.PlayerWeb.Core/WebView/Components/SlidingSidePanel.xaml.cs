using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace VirtualPaper.PlayerWeb.Core.WebView.Components {
    public sealed partial class SlidingSidePanel : UserControl {
        public object PanelContent {
            get => PART_ContentPresenter.Content;
            set => PART_ContentPresenter.Content = value;
        }

        public SlidingSidePanel() {
            InitializeComponent();
        }

        private void SlidingSidePanel_Loaded(object sender, RoutedEventArgs e) {
            if (Parent is FrameworkElement parent) {
                // 初始放在右下角
                double initialY = parent.ActualHeight - ContentArea.Height;
                TranslateXY.Y = initialY;
            }
            Expand();
        }

        private void SlidingSidePanel_SizeChanged(object sender, SizeChangedEventArgs e) {
            CorrectYPositionIfOutOfRange();
        }

        #region Expand / Collapse
        private bool _isExpanded = true;

        private void Toggle() {
            if (_isExpanded) Collapse();
            else Expand();
            _isExpanded = !_isExpanded;
        }

        private void Expand() {
            AnimateX(0);
        }

        private void Collapse() {
            AnimateX(ContentArea.Width);
        }

        private void AnimateX(double to) {
            var animation = new DoubleAnimation {
                To = to,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            Storyboard.SetTarget(animation, TranslateXY);
            Storyboard.SetTargetProperty(animation, "X");

            storyboard.Begin();
        }
        #endregion

        #region Drag logic
        private bool _isDragging = false;
        private bool _isPressed = false;
        private double _pressStartY;
        private double _panelStartY;

        private const double DragThreshold = 3;

        private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e) {
            _isPressed = true;
            _pressStartY = e.GetCurrentPoint(null).Position.Y;
            _panelStartY = TranslateXY.Y;

            HandleArea.CapturePointer(e.Pointer);
        }

        private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e) {
            var pos = e.GetCurrentPoint(null).Position.Y;
            double delta = pos - _pressStartY;

            // 模糊判断移动超过阈值才算拖动
            if (_isPressed && Math.Abs(delta) > DragThreshold) {
                _isDragging = true;
            }

            if ( _isDragging) {
                double newY = _panelStartY + delta;

                if (Parent is FrameworkElement parent) {
                    newY = Math.Clamp(newY, 0, parent.ActualHeight - ContentArea.ActualHeight);
                }

                TranslateXY.Y = newY;
            }
        }

        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e) {
            HandleArea.ReleasePointerCaptures();

            if (!_isDragging) {
                Toggle();
            }

            _isPressed = false;
            _isDragging = false;
        }
        #endregion

        private void CorrectYPositionIfOutOfRange() {
            if (Parent is FrameworkElement parent) {
                double minY = 0;
                double maxY = parent.ActualHeight - ContentArea.ActualHeight;

                // 避免可视区域太小导致 Math.Clamp 报错
                if (maxY < minY) {
                    TranslateXY.Y = minY;
                    return;
                }

                double currentY = TranslateXY.Y;

                // 如果没有越界就保持不动
                if (currentY >= minY && currentY <= maxY)
                    return;

                // 越界时矫正到最近合法位置
                double correctedY = Math.Clamp(currentY, minY, maxY);

                TranslateXY.Y = correctedY;
            }
        }
    }
}
