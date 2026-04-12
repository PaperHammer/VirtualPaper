using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.PlayerWeb.Core.ViewModels;
using VirtualPaper.UIComponent.Utils;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Components.General {
    public sealed partial class GeneralInfoEdit : Page {
        public GeneralInfoEdit() {            
            this.InitializeComponent();
            _viewModel = new GeneralInfoEditViewModel();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            if (e.Parameter is FrameworkPayload payload &&
                payload.TryGet(NaviPayloadKey.IWpBasicData.ToString(), out IWpBasicData wpBasicData)) {                
                _viewModel.InitData(wpBasicData);
            }
        }

        private void TagInput_KeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key == VirtualKey.Enter) {
                string tagText = tagInput.Text.TrimStart().TrimEnd();
                if (_viewModel.TagList.Contains(tagText)) {
                    return;
                }
                tagInput.Text = string.Empty;
                _viewModel.TagList.Add(tagText);
            }
        }

        private void TagDelButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button { Tag: string tag }) {
                _viewModel.TagList.Remove(tag);
            }
        }

        private readonly GeneralInfoEditViewModel _viewModel;
    }
}
