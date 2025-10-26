using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using WinUIEx;

namespace VirtualPaper.UIComponent.Windowing {
    public abstract class ArcWindow : WindowEx { 
        public abstract IReadOnlyList<FrameworkElement> TitleBarChildren { get; }
        public abstract Grid AppRoot { get; }
        public abstract Image AppThemeTransitionImage { get; }
        public abstract Grid AppTitleBar { get; }
        public abstract NavigationView AppNavView { get; }
        public abstract AppSystemBackdrop CurrentBackdrop { get; }

        protected ArcWindow() {
            WindowHelper.TrackWindow(this);
            this.Closed += ArcWindow_Closed;

            _propertyHost = new();
        }

        private void ArcWindow_Closed(object sender, WindowEventArgs args) {
            ThemeHelper.Cleanup();
        }

        private void UpdateThemeIcon() {
            _propertyHost.CurrentThemeIcon = _currentTheme switch {
                AppTheme.Auto => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeAuto"],
                AppTheme.Light => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeLight"],
                AppTheme.Dark => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeDark"],
                _ => (BitmapImage)Application.Current.Resources["NaviIcon_ThemeAuto"]
            };
        }

        protected void AfterRootLoaded() {
            ThemeHelper.RegisterThemeRoot(AppRoot);
            _compositor = ElementCompositionPreview.GetElementVisual(AppRoot).Compositor;
            _isLoaded = true;
        }

        public async Task SetThemeAsync(AppTheme theme = AppTheme.None) {
            if (!_isLoaded || AppRoot == null || AppRoot.ActualWidth <= 0 || AppRoot.ActualHeight <= 0)
                return;

            // 捕获当前界面图像
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(AppRoot);
            AppThemeTransitionImage.Source = bitmap;
            AppThemeTransitionImage.Visibility = Visibility.Visible;
            AppThemeTransitionImage.Opacity = 1.0;

            UpdateTheme(theme);

            // 动画
            var imageVisual = ElementCompositionPreview.GetElementVisual(AppThemeTransitionImage);
            var fadeAnim = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(0f, 1f);
            fadeAnim.InsertKeyFrame(1f, 0f);
            fadeAnim.Duration = TimeSpan.FromMilliseconds(900);
            imageVisual.StartAnimation(nameof(imageVisual.Opacity), fadeAnim);

            await Task.Delay(900);

            AppThemeTransitionImage.Visibility = Visibility.Collapsed;
            AppThemeTransitionImage.Source = null;
        }

        public void UpdateTheme(AppTheme theme) {
            _currentTheme = theme == AppTheme.None ? GetNextTheme(_currentTheme) : theme;

            ThemeHelper.ApplyTheme(_currentTheme);
            UpdateThemeIcon();
        }

        private static AppTheme GetNextTheme(AppTheme current) {
            return current switch {
                AppTheme.Light => AppTheme.Dark,
                AppTheme.Dark => AppTheme.Auto,
                AppTheme.Auto => AppTheme.Light,
                _ => AppTheme.Light
            };
        }

        protected Compositor _compositor;
        protected AppTheme _currentTheme = AppTheme.None;
        protected bool _isLoaded;
        protected readonly PropertyHost _propertyHost;
    }

    public partial class PropertyHost : FrameworkElement {
        public ImageSource CurrentThemeIcon {
            get => (ImageSource)GetValue(CurrentThemeIconProperty);
            set => SetValue(CurrentThemeIconProperty, value);
        }

        public static readonly DependencyProperty CurrentThemeIconProperty =
            DependencyProperty.Register(nameof(CurrentThemeIcon), typeof(ImageSource),
                typeof(PropertyHost), new PropertyMetadata(null));
    }
}
