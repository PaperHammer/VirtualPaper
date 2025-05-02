using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AppSettingsPanel.Views;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppSettings : Page, IAppSettingsPanel {
        public AppSettings() {
            this.InitializeComponent();            

            _selBarItem1 = LanguageUtil.GetI18n(Constants.I18n.AppSettings_SelBarItem1_General);
            _selBarItem2 = LanguageUtil.GetI18n(Constants.I18n.AppSettings_SelBarItem2_Performance);
            _selBarItem3 = LanguageUtil.GetI18n(Constants.I18n.AppSettings_SelBarItem3_System);
            _selBarItem4 = LanguageUtil.GetI18n(Constants.I18n.AppSettings_SelBarItem4_Others);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._windowBridge == null) {
                ContentFrame.CacheSize = 4;
                this._windowBridge = e.Parameter as IWindowBridge;
            }
        }

        #region bridge
        public nint GetWindowHandle() {
            return _windowBridge.GetWindowHandle();
        }

        public INoifyBridge GetNotify() {
            return _windowBridge.GetNotify();
        }

        public void Log(LogType type, object message) {
            _windowBridge.Log(type, message);
        }
        #endregion

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            Type pageType = currentSelectedIndex switch {
                0 => typeof(GeneralSetting),
                1 => typeof(PerformanceSetting),
                2 => typeof(SystemSetting),
                3 => typeof(OthersSetting),
                _ => null,
            };
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            ContentFrame.Navigate(pageType, this, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
        }

        private int _previousSelectedIndex = 0;
        private IWindowBridge _windowBridge;
        public readonly string _selBarItem1;
        public readonly string _selBarItem2;
        public readonly string _selBarItem3;
        public readonly string _selBarItem4;
    }
}
