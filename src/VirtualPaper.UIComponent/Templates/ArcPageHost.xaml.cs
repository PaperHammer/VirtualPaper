using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using VirtualPaper.UIComponent.Feedback;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.UIComponent.Templates {
    [ContentProperty(Name = "RootContent")]
    public sealed partial class ArcPageHost : UserControl {
        public object RootContent {
            get => GetValue(RootContentProperty);
            set => SetValue(RootContentProperty, value);
        }
        public static readonly DependencyProperty RootContentProperty =
            DependencyProperty.Register(nameof(RootContent), typeof(object), typeof(ArcPageHost),
                new PropertyMetadata(null));

        public bool LoadingCancelEnable {
            get => (bool)GetValue(LoadingCancelEnableProperty);
            set => SetValue(LoadingCancelEnableProperty, value);
        }
        public static readonly DependencyProperty LoadingCancelEnableProperty =
            DependencyProperty.Register(nameof(LoadingCancelEnable), typeof(bool), typeof(ArcPageHost),
                new PropertyMetadata(false));

        public bool LoadingProgressbarEnable {
            get => (bool)GetValue(LoadingProgressbarEnableProperty);
            set => SetValue(LoadingProgressbarEnableProperty, value);
        }
        public static readonly DependencyProperty LoadingProgressbarEnableProperty =
            DependencyProperty.Register(nameof(LoadingProgressbarEnable), typeof(bool), typeof(ArcPageHost),
                new PropertyMetadata(false));

        public CancellationTokenSource[] LoadingCtsTokens {
            get => (CancellationTokenSource[])GetValue(LoadingCtsTokensProperty);
            set => SetValue(LoadingCtsTokensProperty, value);
        }
        public static readonly DependencyProperty LoadingCtsTokensProperty =
            DependencyProperty.Register(nameof(LoadingCtsTokens), typeof(CancellationTokenSource[]), typeof(ArcPageHost),
                new PropertyMetadata(null));

        public int LoadingTotalValue {
            get => (int)GetValue(LoadingTotalValueProperty);
            set => SetValue(LoadingTotalValueProperty, value);
        }
        public static readonly DependencyProperty LoadingTotalValueProperty =
            DependencyProperty.Register(nameof(LoadingTotalValue), typeof(int), typeof(ArcPageHost),
                new PropertyMetadata(0));

        public int LoadingCurValue {
            get => (int)GetValue(LoadingCurValueProperty);
            set => SetValue(LoadingCurValueProperty, value);
        }
        public static readonly DependencyProperty LoadingCurValueProperty =
            DependencyProperty.Register(nameof(LoadingCurValue), typeof(int), typeof(ArcPageHost),
                new PropertyMetadata(0));

        public Visibility LoadingVisibility {
            get => (Visibility)GetValue(LoadingVisibilityProperty);
            set => SetValue(LoadingVisibilityProperty, value);
        }
        public static readonly DependencyProperty LoadingVisibilityProperty =
            DependencyProperty.Register(nameof(LoadingVisibility), typeof(Visibility), typeof(ArcPageHost),
                new PropertyMetadata(Visibility.Collapsed));

        public Grid ContentGrid => this.PART_ContentGrid;
        public ContentPresenter ContentPresenterHost => this.PART_ContentPresenterHost;
        public Loading LoadingControlHost => this.PART_LoadingControlHost;

        public ArcPageHost() {
            this.InitializeComponent();
        }
    }
}
