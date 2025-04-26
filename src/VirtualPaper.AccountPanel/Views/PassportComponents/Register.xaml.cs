using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views.PassportComponents {
    public sealed partial class Register : Page {
        public Register() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            _viewModel = ObjectProvider.GetRequiredService<RegisterViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
            this._passport ??= e.Parameter as Passport;
        }

        private async void BtnRequestCode_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.SendEmailCodeAsync();
        }

        private async void Register_Clicked(object sender, RoutedEventArgs e) {
            var userInfo = await _viewModel.RegsiterAsync();
            if (userInfo == null) return;
            _passport.ChangePanelState(AccountPanelState.UserCenter, userInfo);
        }

        private void BtnBack_Clicked(object sender, RoutedEventArgs e) {
            _passport.ChangePanelState(AccountPanelState.Login, null);
        }

        private Passport _passport;
        internal RegisterViewModel _viewModel;
    }
}
