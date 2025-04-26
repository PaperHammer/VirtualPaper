using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class UserCenterViewModel : ObservableObject {
        public string Account_Logout { get; set; }

        private string _sign;
        public string Sign {
            get { return _sign; }
            set { _sign = value; OnPropertyChanged(); }
        }

        private UserInfo _user;
        public UserInfo User {
            get { return _user; }
            set {
                if (value == null || _user == value) return;
                _user = value;
                Sign = value.Sign == null || value.Sign.Length == 0 ?
                    LanguageUtil.GetI18n(nameof(Constants.I18n.Account_DefaultSign)) : value.Sign;
                OnPropertyChanged();
            }
        }

        public UserCenterViewModel(IAccountClient accountClient) {
            _accountClient = accountClient;
            InitText();
        }

        private void InitText() {
            Account_Logout = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Logout));
        }

        internal async Task SubmmitEditAsync(UserInfoEditViewModel editViewModel) {
            if (User == editViewModel.User) return;
            if (!editViewModel.IsUsernameOk) {
                Account.Instance.GetNotify().ShowMsg(
                    true,
                    nameof(Constants.I18n.Account_Username_InvalidTip_Sim),
                    InfoBarType.Error,
                    key: nameof(Constants.I18n.Account_Username_InvalidTip_Sim),
                    isAllowDuplication: false);
                return;
            }
            if (!editViewModel.IsSignOk) {
                Account.Instance.GetNotify().ShowMsg(
                    true,
                    nameof(Constants.I18n.Account_Usersign_InvalidTip_Sim),
                    InfoBarType.Error,
                    key: nameof(Constants.I18n.Account_Usersign_InvalidTip_Sim),
                    isAllowDuplication: false);
                return;
            }

            try {
                Account.Instance.GetNotify().Loading(false, false);
                var res = await _accountClient.UpdateUserInfoAsync(editViewModel.User);
                if (!res.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        res.Message,
                        InfoBarType.Error,
                        key: res.Message,
                        isAllowDuplication: false);
                    return;
                }
                User = DataAssist.FromGrpcUserInfo(res.User);
            }
            catch (Exception) {

                throw;
            }
            finally {
                Account.Instance.GetNotify().Loaded();
            }
        }

        internal async Task<bool> LogoutAsync() {
            try {
                Account.Instance.GetNotify().Loading(false, false);
                //var response = await _accountClient.LogoutAsync();
                //if (!response.Success) {
                //    Account.Instance.GetNotify().ShowMsg(
                //        true,
                //        response.Message,
                //        InfoBarType.Error,
                //        key: response.Message,
                //        isAllowDuplication: false);
                //}

                //return response.Success;
                return true;
            }
            catch (System.Exception ex) {
                Account.Instance.GetNotify().ShowMsg(
                    true,
                    nameof(Constants.I18n.InnerErr),
                    InfoBarType.Error,
                    key: nameof(Constants.I18n.InnerErr),
                    isAllowDuplication: false);
            }
            finally {
                Account.Instance.GetNotify().Loaded();
            }

            return false;
        }

        private readonly IAccountClient _accountClient;
    }
}
