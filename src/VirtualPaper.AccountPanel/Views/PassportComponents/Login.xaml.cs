using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.DI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views.PassportComponents {
    public sealed partial class Login : Page {
        public Login() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            _viewModel = ObjectProvider.GetRequiredService<LoginViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
            this._passport ??= e.Parameter as Passport;
        }

        private void BtnGoToRegister_Clicked(object sender, RoutedEventArgs e) {
            _passport.ChangePanelState(AccountPanelState.Register, null);
        }

        private async void BtnLogin_Clicked(object sender, RoutedEventArgs e) {
            bool op = await _viewModel.LoginAsync();
            if (op) {
                _passport.ChangePanelState(AccountPanelState.UserCenter, null);
            }
        }

        private Passport _passport;
        internal LoginViewModel _viewModel;
    }
}
