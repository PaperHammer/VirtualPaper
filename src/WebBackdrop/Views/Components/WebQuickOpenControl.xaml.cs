using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Workloads.Creation.WebBackdrop.Views.Components {
    public sealed partial class WebQuickOpenControl : UserControl {
        public AutoSuggestBox Input => quickOpenBox;

        public WebQuickOpenControl() {
            InitializeComponent();
            // AutoSuggestBox 会先消费 Esc 用于关闭建议列表，handledEventsToo 确保取消逻辑仍能执行。
            quickOpenBox.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(QuickOpenBox_KeyDown), true);
        }

        private void QuickOpenBox_KeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key != VirtualKey.Escape) return;

            e.Handled = true;
            quickOpenBox.Text = string.Empty;
            quickOpenBox.IsSuggestionListOpen = false;
        }
    }
}
