using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Container {
    public sealed partial class SlidingSidePanel : UserControl {
        public object PanelContent {
            get => PART_ContentPresenter.Content;
            set => PART_ContentPresenter.Content = value;
        }

        public SlidingSidePanel() {
            InitializeComponent();
            Loaded += SlidingSidePanel_Loaded;
        }

        private void SlidingSidePanel_Loaded(object sender, RoutedEventArgs e) {
            Expand();
        }

        #region Expand / Collapse Animations
        private void Toggle() {
            if (_isExpanded)
                Collapse();
            else
                Expand();

            _isExpanded = !_isExpanded;
        }

        private void Expand() {
            AnimateX(0);
            _isExpanded = true;
        }

        private void Collapse() {
            AnimateX(-ContentArea.Width);
        }

        private void AnimateX(double to) {
            var animation = new DoubleAnimation {
                To = to,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            Storyboard.SetTarget(animation, TranslateX);
            Storyboard.SetTargetProperty(animation, "X");

            storyboard.Begin();
        }
        #endregion

        #region Drag logic
        private async void Handle_PointerPressed(object sender, PointerRoutedEventArgs e) {
            _dragStartOffsetY = e.GetCurrentPoint(null).Position.Y;
            _panelStartY = Canvas.GetTop(this);

            _isDragging = false;

            // 长按开始拖动（300ms）
            await Task.Delay(300);

            if (e.Pointer.IsInContact) {
                _isDragging = true;
            }
            else {
                Toggle();
            }
        }

        private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e) {
            if (!_isDragging) return;

            var pos = e.GetCurrentPoint(null).Position.Y;
            double delta = pos - _dragStartOffsetY;
            double newY = _panelStartY + delta;

            // 限制范围不超出 Page 高度
            if (Parent is FrameworkElement parent) {
                newY = Math.Clamp(newY, 0, parent.ActualHeight - HandleArea.Height);
            }

            Canvas.SetTop(this, newY);
        }

        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e) {
            if (!_isDragging) {
                Toggle();
            }

            _isDragging = false;
        }
        #endregion

        private bool _isExpanded = true;
        private bool _isDragging = false;
        private double _dragStartOffsetY;
        private double _panelStartY;
    }
}
