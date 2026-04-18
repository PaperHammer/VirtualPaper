using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Text {
    /// <summary>
    /// 极简 Markdown 文本块组件 (内部包裹 RichTextBlock，支持高度自适应)
    /// </summary>
    public sealed partial class ArcMarkdownTextBlock : UserControl {        
        public string Markdown {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.Register(
                nameof(Markdown),
                typeof(string),
                typeof(ArcMarkdownTextBlock),
                new PropertyMetadata(string.Empty, OnMarkdownChanged));

        public ArcMarkdownTextBlock() {
            _richTextBlock = new RichTextBlock {
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                // 使用系统的主题画刷，确保跟随亮暗色模式自动切换
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };

            // 将 RichTextBlock 设置为此 UserControl 的唯一内容
            this.Content = _richTextBlock;

            // 自身背景设为透明，完美融入外部的亚克力/云母材质
            this.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ArcMarkdownTextBlock textBlock) {
                var newText = e.NewValue as string;
                SimpleMarkdownRenderer.Render(newText, textBlock._richTextBlock);
            }
        }

        private readonly RichTextBlock _richTextBlock;
    }
}
