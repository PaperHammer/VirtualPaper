using System;
using System.Collections.ObjectModel;
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
        public virtual NavigationView? AppNavView { get; }
        public virtual bool IsMainWindow => false;
        protected virtual bool IsNeedTrack => true;
        public abstract ArcWindowHost ContentHost { get; }
        public abstract ArcWindowManagerKey Key { get; }
        protected PropertyHost? PropertyHost => _propertyHost;
        public ObservableCollection<GlobalMsgInfo> InfobarMessages { get; } = [];
        public bool IsActive => _isActive ?? false;
        protected virtual string? AssetsIcon => "Assets/virtualpaper.ico";

        public ArcWindow(AppTheme appTheme = AppTheme.Auto, AppSystemBackdrop systemBackdrop = default) {            
            if (IsMainWindow) {
                _propertyHost = new();
                ArcThemeUtil.SetMainWindowAppTheme(appTheme);
                ArcThemeUtil.SetMainWindowBackdrop(systemBackdrop);
            }

            this.Activated += ArcWindow_Activated;
            this.AppWindow.Closing += AppWindow_Closing;
        }

        private void ArcWindow_Activated(object sender, WindowActivatedEventArgs args) {
            bool isActive = args.WindowActivationState != WindowActivationState.Deactivated;
            if (_isActive == isActive) return;
            _isActive = isActive;

            ArcWindowTitleBarUtil.UpdateTitleBar(this, ArcThemeUtil.GetFormatMainWindowTheme(), isActive);
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) {
            // 点击关闭按钮时，主窗口会卡住几秒，才关闭窗口 todo（优化）
            this.Hide();
            // Window.Closed 的触发时机并不保证晚于 Activated/VisibilityChanged
            this.Activated -= ArcWindow_Activated;
            // 避免子窗口的关闭导致 ArcThemeUtil 清理
            if (IsMainWindow) {
                ArcWindowManager.Cleanup();
                ArcThemeUtil.Cleanup();
            }
        }

        private async void AppRoot_Loaded(object sender, RoutedEventArgs e) {
            _compositor = ElementCompositionPreview.GetElementVisual(this.ContentHost.AppRoot).Compositor;
            _isLoaded = true;
            await SetThemeAsync(); // todo 待优化
        }

        protected void InitializeWindow() {
            this.ContentHost.AppRoot.Loaded += AppRoot_Loaded;            

            if (IsNeedTrack) {
                ArcWindowManager.TrackWindow(Key, this);
            }
            SetWindowStartupPosition();
            SetWindowStyle();
            SetWindowTitleBar(); // todo 待优化
        }

        #region theme
        protected void UpdateThemeFromThemeBtnClick(AppTheme theme) {
            if (!IsMainWindow) return;
            ArcThemeUtil.UpdateThemeGlobal(theme);
        }

        private void UpdateThemeIcon() {
            if (!IsMainWindow || _propertyHost == null) return;

            _propertyHost.ThemeIconKey = ArcThemeUtil.MainWindowAppTheme switch {
                AppTheme.Light => "NaviIcon_ThemeLight",
                AppTheme.Dark => "NaviIcon_ThemeDark",
                _ => "NaviIcon_ThemeAuto"
            };
        }

        public async Task SetThemeAsync() {
            if (!_isLoaded || _compositor == null || this.ContentHost.AppRoot == null || this.ContentHost.AppRoot.ActualWidth <= 0 || this.ContentHost.AppRoot.ActualHeight <= 0)
                return;

            UpdateThemeIcon();

            // 捕获当前界面图像
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(this.ContentHost.AppRoot);
            this.ContentHost.AppThemeTransitionImage.Source = bitmap;
            this.ContentHost.AppThemeTransitionImage.Visibility = Visibility.Visible;
            this.ContentHost.AppThemeTransitionImage.Opacity = 1.0;

            UpdateTheme();

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

        private void UpdateTheme() {
            ArcThemeUtil.ApplyTheme(this.ContentHost);
            ArcWindowManager.UpdateWindowVisualState(this);
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
                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(this.ContentHost.AppTitleBar);
                this.AppWindow.SetIcon(AssetsIcon);
                this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            }
            else {
                this.ContentHost.AppTitleBar.Visibility = Visibility.Collapsed;
                this.UseImmersiveDarkModeEx(ArcThemeUtil.MainWindowAppTheme == AppTheme.Dark);
            }
        }

        private void SetWindowStyle() {
            this.SystemBackdrop = ArcThemeUtil.MainWindowBackdrop switch {
                AppSystemBackdrop.Mica => new MicaBackdrop(),
                AppSystemBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => default,
            };
        }
        #endregion

        private bool _isLoaded;
        private Compositor _compositor = null!;
        private bool? _isActive = null;
        private readonly PropertyHost? _propertyHost;
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