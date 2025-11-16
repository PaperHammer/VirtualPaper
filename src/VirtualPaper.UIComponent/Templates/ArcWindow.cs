using System;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;
using WinUIEx;

namespace VirtualPaper.UIComponent.Templates {
    public abstract partial class ArcWindow : WindowEx {
        public AppTheme CurrentTheme => _currentTheme;
        public AppSystemBackdrop CurrentBackdrop => _currentBackdrop;
        public virtual NavigationView? AppNavView { get; }
        public abstract ArcWindowHost ContentHost { get; }
        protected PropertyHost PropertyHost => _propertyHost;

        public ArcWindow(AppTheme initialTheme = AppTheme.Auto, AppSystemBackdrop systemBackdrop = default) {
            _currentTheme = initialTheme;
            _currentBackdrop = systemBackdrop;
            _propertyHost = new();

            this.Closed += ArcWindow_Closed;
        }

        private void ArcWindow_Closed(object sender, WindowEventArgs args) {
            ThemeHelper.Cleanup();
        }

        private async void AppRoot_Loaded(object sender, RoutedEventArgs e) {
            AfterRootLoaded();
            await SetThemeAsync(_currentTheme);
        }

        protected void InitializeWindow() {
            this.ContentHost.AppRoot.Loaded += AppRoot_Loaded;

            WindowHelper.TrackWindow(this);
            SetWindowStartupPosition();
            SetWindowStyle();
            SetWindowTitleBar();
        }

        #region theme
        private void UpdateThemeIcon() {
            _propertyHost.ThemeIconKey = _currentTheme switch {
                AppTheme.Light => "NaviIcon_ThemeLight",
                AppTheme.Dark => "NaviIcon_ThemeDark",
                _ => "NaviIcon_ThemeAuto"
            };
        }

        private void AfterRootLoaded() {
            ThemeHelper.RegisterThemeRoot(this.ContentHost.AppRoot);
            _compositor = ElementCompositionPreview.GetElementVisual(this.ContentHost.AppRoot).Compositor;
            _isLoaded = true;
        }

        public async Task SetThemeAsync(AppTheme theme = AppTheme.None) {
            if (!_isLoaded || _compositor == null || this.ContentHost.AppRoot == null || this.ContentHost.AppRoot.ActualWidth <= 0 || this.ContentHost.AppRoot.ActualHeight <= 0)
                return;

            // 捕获当前界面图像
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(this.ContentHost.AppRoot);
            this.ContentHost.AppThemeTransitionImage.Source = bitmap;
            this.ContentHost.AppThemeTransitionImage.Visibility = Visibility.Visible;
            this.ContentHost.AppThemeTransitionImage.Opacity = 1.0;

            UpdateTheme(theme);

            // 动画
            var imageVisual = ElementCompositionPreview.GetElementVisual(this.ContentHost.AppThemeTransitionImage);
            var fadeAnim = _compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(0f, 1f);
            fadeAnim.InsertKeyFrame(1f, 0f);
            fadeAnim.Duration = TimeSpan.FromMilliseconds(600);
            imageVisual.StartAnimation(nameof(imageVisual.Opacity), fadeAnim);

            await Task.Delay(600);

            this.ContentHost.AppThemeTransitionImage.Visibility = Visibility.Collapsed;
            this.ContentHost.AppThemeTransitionImage.Source = null;
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
        #endregion

        #region window property
        protected virtual void SetWindowStartupPosition() {
            DisplayArea displayArea = SystemUtil.GetDisplayArea(this, DisplayAreaFallback.Nearest);
            if (displayArea is not null) {
                var centeredPosition = this.AppWindow.Position;
                centeredPosition.X = (displayArea.WorkArea.Width - this.AppWindow.Size.Width) / 2;
                centeredPosition.Y = (displayArea.WorkArea.Height - this.AppWindow.Size.Height) / 2;
                this.AppWindow.Move(centeredPosition);
            }
        }

        private void SetWindowTitleBar() {
            if (AppWindowTitleBar.IsCustomizationSupported()) {
                var titleBar = this.AppWindow.TitleBar;

                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(this.ContentHost.AppTitleBar);
                this.AppWindow.SetIcon("Assets/virtualpaper.ico");
                titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }
            else {
                this.ContentHost.AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(_currentTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            this.SystemBackdrop = _currentBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }
        #endregion

        private AppTheme _currentTheme;
        private bool _isLoaded;
        private Compositor _compositor = null!;
        private readonly AppSystemBackdrop _currentBackdrop;
        private readonly PropertyHost _propertyHost;
    }

    public partial class PropertyHost : FrameworkElement {
        public string ThemeIconKey {
            get => (string)GetValue(ThemeIconKeyProperty);
            set => SetValue(ThemeIconKeyProperty, value);
        }
        public static readonly DependencyProperty ThemeIconKeyProperty =
            DependencyProperty.Register(nameof(ThemeIconKey), typeof(string),
                typeof(PropertyHost), new PropertyMetadata(null));
    }
}