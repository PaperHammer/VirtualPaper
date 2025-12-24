using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualPaper.PlayerWeb.Core.WebView.Components.General;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Components {
    public sealed partial class DataConfig : UserControl {
        //public ArcPageContext PageContext { get; set; } = null!;
        public NavigationPayload? Payload { get; set; }

        public DataConfig() {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            if (isInit) return;
            isInit = true;
            BuildTabs();
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs _) {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
            string? tag = selectedItem.Tag?.ToString();

            Type pageType = tag switch {
                nameof(DataConfigTab.GeneralEffect) => typeof(GeneralEffect),
                nameof(DataConfigTab.GeneralInfo) => typeof(GeneralInfo),
                nameof(DataConfigTab.GeneralInfoEdit) => typeof(GeneralInfoEdit),
                _ => throw new NotImplementedException(),
            };
            var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

            ContentFrame.Navigate(pageType, Payload, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

            _previousSelectedIndex = currentSelectedIndex;
        }
        
        private void BuildTabs() {
            if (Payload != null && Payload.TryGet(NaviPayLoadKey.AvailableConfigTab.ToString(), out DataConfigTab availableTab)) {

                void TryAdd(DataConfigTab tab, string title) {
                    if (availableTab.HasFlag(tab)) {
                        SelBar.Items.Add(new SelectorBarItem {
                            Tag = tab,
                            Text = LanguageUtil.GetI18n(title)
                        });
                    }
                }

                TryAdd(DataConfigTab.GeneralEffect, "Webview_SelBarItem1_GeneralEffect");
                TryAdd(DataConfigTab.GeneralInfo, "Webview_SelBarItem2_GeneralInfo");
                TryAdd(DataConfigTab.GeneralInfoEdit, "Webview_SelBarItem3_GeneralInfoEdit");

                if (SelBar.Items.Count > 0) {
                    SelBar.SelectedItem = SelBar.Items[0];
                }
            }
        }

        private int _previousSelectedIndex = 0;
        private bool isInit;
    }
}
