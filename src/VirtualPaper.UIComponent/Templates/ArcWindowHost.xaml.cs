using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using VirtualPaper.UIComponent.Styles;

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

#if DEBUG
            AppSubTitleTextBlock.Visibility = Visibility.Visible;
#else
            AppSubTitleTextBlock.Visibility = Visibility.Collapsed;
#endif
        }
    }
}
