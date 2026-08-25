using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.UIComponent.Styles;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Templates {
    [ContentProperty(Name = "RootContent")]
    public sealed partial class ArcWindowHost : UserControl {
        public string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ArcWindowHost),
                new PropertyMetadata("Virtual Paper"));

        public string TitleBarIconKey {
            get => (string)GetValue(TitleBarIconKeyProperty);
            set => SetValue(TitleBarIconKeyProperty, value);
        }
        public static readonly DependencyProperty TitleBarIconKeyProperty =
            DependencyProperty.Register(nameof(TitleBarIconKey), typeof(string), typeof(ArcWindowHost),
                new PropertyMetadata("AppIcon"));

        public object RootContent {
            get => GetValue(RootContentProperty);
            set => SetValue(RootContentProperty, value);
        }
        public static readonly DependencyProperty RootContentProperty =
            DependencyProperty.Register(nameof(RootContent), typeof(object), typeof(ArcWindowHost),
                new PropertyMetadata(null));

        public Grid AppRoot => this.PART_RootGrid;
        public Grid AppTitleBar => this.PART_RootTitleBar;
        public ArcImageIcon AppTitleBarIcon => this.PART_RootTitleBarIcon;
        public TextBlock AppTitleTextBlock => this.PART_RootTitleTextBlock;
        public TextBlock AppSubTitleTextBlock => this.PART_RootSubTitleTextBlock;
        public ContentPresenter AppRootContent => this.PART_RootRootContent;
        public Image AppThemeTransitionImage => this.PART_RootThemeTransitionImage;
        public IReadOnlyList<FrameworkElement> TitleBarChildren => [AppTitleTextBlock, AppSubTitleTextBlock];

        public ArcWindowHost() {
            this.InitializeComponent();
            PART_RootGrid.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(Root_PreviewKeyDown), true);
            PART_RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Root_KeyDown), true);

#if DEBUG
            AppSubTitleTextBlock.Visibility = Visibility.Visible;
#else
            AppSubTitleTextBlock.Visibility = Visibility.Collapsed;
#endif
        }

        private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Handled) return;

            var isTextInput = IsTextInput(e.OriginalSource as DependencyObject);
            if (e.Key == VirtualKey.Escape && !isTextInput) {
                e.Handled = true;
                QueueFocusSink();
                return;
            }

            // Tab/F6 不属于文本编辑，在子控件响应前截止，避免转移焦点。
            if (IsGlobalNavigationKey(e.Key)) {
                e.Handled = true;
                return;
            }

            // 文本输入仍可移动光标、换行和输入空格；其他控件不响应隐式键盘操作。
            if (IsControlInteractionKey(e.Key) && !isTextInput) {
                e.Handled = true;
            }
        }

        private void Root_KeyDown(object sender, KeyRoutedEventArgs e) {
            // 先让输入控件执行各自的取消，再把焦点移到不可见的窗口级落点。
            if (e.Key == VirtualKey.Escape) {
                e.Handled = true;
                QueueFocusSink();
                return;
            }

            // 输入控件未消费的边界方向键在窗口边界截止，避免焦点外逸。
            if (!e.Handled && (IsGlobalNavigationKey(e.Key) || IsControlInteractionKey(e.Key))) {
                e.Handled = true;
            }
        }

        private void QueueFocusSink() {
            // 等当前按键路由和控件布局结束后再聚焦，覆盖 WinUI 的默认焦点回退。
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => { _ = FocusManager.TryFocusAsync(PART_KeyboardFocusSink, FocusState.Programmatic); });
        }

        /// <summary>
        /// 立即接管焦点，避免输入控件退出时 WinUI 先回退到附近控件并绘制一帧焦点框。
        /// </summary>
        public void FocusKeyboardSink() {
            PART_KeyboardFocusSink.Focus(FocusState.Programmatic);
        }

        private static bool IsGlobalNavigationKey(VirtualKey key) => key is
            VirtualKey.Tab or
            VirtualKey.F6;

        private static bool IsControlInteractionKey(VirtualKey key) => key is
            VirtualKey.Escape or
            VirtualKey.Enter or
            VirtualKey.Space or
            VirtualKey.Left or
            VirtualKey.Right or
            VirtualKey.Up or
            VirtualKey.Down or
            VirtualKey.Home or
            VirtualKey.End or
            VirtualKey.PageUp or
            VirtualKey.PageDown;

        private static bool IsTextInput(DependencyObject? current) {
            while (current != null) {
                if (current is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or NumberBox or WebView2) {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
