using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AppSettingsPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OthersSetting : Page {
        public OthersSetting() {
            this.InitializeComponent();
            this.Unloaded += OthersSetting_Unloaded;
            _viewModel = AppServiceLocator.Services.GetRequiredService<OtherSettingViewModel>();
            this.DataContext = _viewModel;            
        }

        private void OthersSetting_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            this.DataContext = null;
            this.Unloaded -= OthersSetting_Unloaded;
        }

        private async void LegalDocument_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            if (_showingLegalDocument || sender is not HyperlinkButton { Content: TextBlock label, Tag: string section }) return;
            _showingLegalDocument = true;
            try { await LegalDocumentDialog.ShowAsync(this, label.Text, section); }
            catch (System.Exception ex) {
                ArcLog.GetLogger<OthersSetting>().Error(
                    $"Could not open legal document '{section}' for '{LanguageUtil.CurrentLanguage}' from '{System.AppContext.BaseDirectory}'.", ex);
                GlobalMessageUtil.ShowException(ex);
            }
            finally { _showingLegalDocument = false; }
        }

        private readonly OtherSettingViewModel _viewModel;
        private bool _showingLegalDocument;
    }
}
