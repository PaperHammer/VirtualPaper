using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.AppSettingsPanel.Views;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppSettings : ArcPage {
        public override ArcPageContext Context { get; set; }
        public override Type PageType => typeof(AppSettings);

        public AppSettings() {
            this.InitializeComponent();
            Context = new ArcPageContext(this);
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            Type pageType = currentSelectedIndex switch {
                0 => typeof(GeneralSetting),
                1 => typeof(PerformanceSetting),
                2 => typeof(SystemSetting),
                3 => typeof(OthersSetting),
                _ => throw new NotImplementedException(),
            };
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            ContentFrame.Navigate(pageType, this, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
        }

        private int _previousSelectedIndex = 0;
    }
}
