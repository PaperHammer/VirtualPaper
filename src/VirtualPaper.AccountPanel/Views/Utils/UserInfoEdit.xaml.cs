using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.AccountPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.AccountPanel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.AccountPanel.Views.Utils {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserInfoEdit : UserControl {
        public UserInfoEdit(UserInfo user) {
            this.InitializeComponent();

            _viewModel = new UserInfoEditViewModel(user);
        }

        private async void BtnChangeImg_Click(object sender, RoutedEventArgs e) {
            var storage = await WindowsStoragePickers.PickFilesAsync(Account.Instance.GetWindowHandle(), FileFilter.AvatarFilter);
            if (storage.Length < 1) return;
            _viewModel.TrySetAvatar(storage[0].Path);
        }

        private void TxbSign_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args) {
            _viewModel.IsSignOk = ComplianceUtil.IsValidSign(sender.Text);
        }

        private void TxbName_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args) {
            _viewModel.IsUsernameOk = ComplianceUtil.IsValidUserName(sender.Text);
        }

        internal UserInfoEditViewModel GetViewModel() {
            return _viewModel;
        }

        private readonly UserInfoEditViewModel _viewModel;
    }
}
