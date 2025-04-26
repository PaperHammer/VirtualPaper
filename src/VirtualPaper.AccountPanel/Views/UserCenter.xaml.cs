using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.AccountPanel.ViewModels;
using VirtualPaper.AccountPanel.Views.Utils;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.Bridge.Base;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserCenter : Page, IAccountPanelBridge {
        public UserCenter() {
            this.InitializeComponent();

            _selBarItem1 = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_SelBarItem1_Cloud));
            _selBarItem2 = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_SelBarItem2_Star));
            _selBarItem3 = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_SelBarItem3_Upload));
        }

        #region nav
        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            _viewModel = ObjectProvider.GetRequiredService<UserCenterViewModel>(ObjectLifetime.Singleton, ObjectLifetime.Singleton);
            _viewModel.User ??= (e.Parameter as IAccountPanelBridge).GetSharedData() as UserInfo;
        }
        #endregion

        private async void EditUserInfoButton_Click(object sender, RoutedEventArgs e) {
            UserInfoEdit edit = new(_viewModel.User.Clone());
            var res = await Account.Instance.GetDialog().ShowDialogWithoutTitleAsync(
                edit,
                LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm)),
                LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel)));
            if (res == DialogResult.Primary) {
                await _viewModel.SubmmitEditAsync(edit.GetViewModel());
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e) {
            var op = await _viewModel.LogoutAsync();
            if (!op) return;
            _viewModel.User = null;
            _viewModel = null;
            Account.Instance.ChangePanelState(AccountPanelState.Passport, null);
        }

        private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) {

        }

        #region bridge
        public void ChangePanelState(AccountPanelState nextPanel, object data) {
            _sharedData = data;
        }

        public object GetSharedData() => _sharedData;

        public IDialogService GetDialog() => Account.Instance.GetDialog();

        public INoifyBridge GetNotify() => Account.Instance.GetNotify();

        public nint GetWindowHandle() => Account.Instance.GetWindowHandle();

        public void Log(LogType type, object message) => Account.Instance.Log(type, message);
        #endregion

        private UserCenterViewModel _viewModel;
        private object _sharedData;
        public readonly string _selBarItem1;
        public readonly string _selBarItem2;
        public readonly string _selBarItem3;
    }
}
