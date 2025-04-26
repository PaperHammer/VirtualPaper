using System;
using System.Threading.Tasks;
using Octokit;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class RegisterViewModel : ObservableObject {
        private string _email = string.Empty;
        public string Email {
            get { return _email; }
            set { _email = value; IsEmailOk = ComplianceUtil.IsValidEmail(value); }
        }

        public string Code { get; set; } = string.Empty;

        private string _username = string.Empty;
        public string Username {
            get { return _username; }
            set { _username = value; IsUsernameOk = ComplianceUtil.IsValidUserName(value); }
        }

        private string _pwd = string.Empty;
        public string Pwd {
            get { return _pwd; }
            set { _pwd = value; IsPwdOk = ComplianceUtil.IsValidPwd(value); }
        }

        private string _confirmPwd = string.Empty;
        public string ConfirmPwd {
            get { return _confirmPwd; }
            set { _confirmPwd = value; IsConfirmPwdOk = Pwd == value; }
        }

        private bool _isEmailOk;
        public bool IsEmailOk {
            get { return _isEmailOk; }
            set {
                _isEmailOk = value;
                IsOk = _isEmailOk && _isUsernameOk && _isPwdOk && _isConfirmPwdOk;
                IsRequestCodeButtonEnabled = value && _remainingSeconds == 0;
                OnPropertyChanged();
            }
        }

        private bool _isUsernameOk;
        public bool IsUsernameOk {
            get { return _isUsernameOk; }
            set { _isUsernameOk = value; IsOk = _isEmailOk && _isUsernameOk && _isPwdOk && _isConfirmPwdOk; OnPropertyChanged(); }
        }

        private bool _isPwdOk;
        public bool IsPwdOk {
            get { return _isPwdOk; }
            set { _isPwdOk = value; IsOk = _isEmailOk && _isUsernameOk && _isPwdOk && _isConfirmPwdOk; OnPropertyChanged(); }
        }


        private bool _isConfirmPwdOk;
        public bool IsConfirmPwdOk {
            get { return _isConfirmPwdOk; }
            set { _isConfirmPwdOk = value; IsOk = _isEmailOk && _isUsernameOk && _isPwdOk && _isConfirmPwdOk; OnPropertyChanged(); }
        }

        private bool _isOk;
        public bool IsOk {
            get { return _isOk; }
            set { _isOk = value; OnPropertyChanged(); }
        }

        private bool _isRequestCodeButtonEnabled = false;
        public bool IsRequestCodeButtonEnabled {
            get => _isRequestCodeButtonEnabled;
            set {
                if (_isRequestCodeButtonEnabled != value) {
                    _isRequestCodeButtonEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Account_Email_InvalidTip { get; private set; }
        public string Account_Pwd_InvalidTip { get; private set; }
        public string Account_ConfirmPwd_InvalidTip { get; private set; }
        public string Account_Username_InvalidTip { get; private set; }
        public string Account_RegisterWithEmail { get; private set; }
        public string Account_EmailText { get; private set; }
        public string Account_EmailTextWithColon { get; private set; }
        public string Account_PwdText { get; private set; }
        public string Account_PwdTextWithColon { get; private set; }
        public string Account_ConfirmPwdText { get; private set; }
        public string Account_ConfirmPwdTextWithColon { get; private set; }
        public string Account_CodeText { get; private set; }
        public string Account_CodeTextWithColon { get; private set; }
        public string Account_UsernameText { get; private set; }
        public string Account_UsernameTextWithColon { get; private set; }
        public string Account_RegisterText { get; private set; }
        public string Account_BackText { get; private set; }

        private string _account_RequestCodeText;
        public string Account_RequestCodeText {
            get => _account_RequestCodeText;
            set { _account_RequestCodeText = value; OnPropertyChanged(); }
        }

        public RegisterViewModel(IAccountClient accountClient) {
            _accountClient = accountClient;
            InitText();
        }

        private void InitText() {
            Account_Email_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Email_InvalidTip));
            Account_Pwd_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Pwd_InvalidTip));
            Account_ConfirmPwd_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_ConfirmPwd_InvalidTip));
            Account_Username_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Username_InvalidTip));
            Account_RegisterWithEmail = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_RegisterWithEmail));
            Account_EmailText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_EmailText));
            Account_EmailTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_EmailTextWithColon));
            Account_PwdText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_PwdText));
            Account_PwdTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_PwdTextWithColon));
            Account_ConfirmPwdText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_ConfirmPwdText));
            Account_ConfirmPwdTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_ConfirmPwdTextWithColon));
            Account_CodeText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_CodeText));
            Account_CodeTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_CodeTextWithColon));
            Account_UsernameText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_UsernameText));
            Account_UsernameTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_UsernameTextWithColon));
            Account_RegisterText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_RegisterText));
            Account_BackText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_BackText));
            _requestCodeText = Account_RequestCodeText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_RequestCodeText));
        }

        internal async Task<bool> SendEmailCodeAsync() {
            try {
                Account.Instance.GetNotify().Loading(false, false);
                var response = await _accountClient.SendEmailCodeAsync(Email);
                if (!response.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                }
                else {
                    StartCountdown();
                }

                return response.Success;
            }
            catch (Exception) {
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

        private async void StartCountdown() {
            IsRequestCodeButtonEnabled = false;
            _remainingSeconds = 60;
            while (_remainingSeconds > 0) {
                Account_RequestCodeText = $"{_remainingSeconds}";
                await Task.Delay(1000);
                _remainingSeconds--;
            }
            ResetButtonState();
        }

        private void ResetButtonState() {
            Account_RequestCodeText = _requestCodeText;
            IsRequestCodeButtonEnabled = IsEmailOk && _remainingSeconds == 0;
        }

        internal async Task<UserInfo> RegsiterAsync() {
            try {
                Account.Instance.GetNotify().Loading(false, false);
                var response = await _accountClient.RegisterAsync(Email, Username, Code, Pwd, ConfirmPwd);
                if (!response.Success) {
                    Account.Instance.GetNotify().ShowMsg(
                        true,
                        response.Message,
                        InfoBarType.Error,
                        key: response.Message,
                        isAllowDuplication: false);
                    return null;
                }
                StartCountdown();

                return response.Success ? DataAssist.FromGrpcUserInfo(response.User) : null;
            }
            catch (Exception) {
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

            return null;
        }

        private readonly IAccountClient _accountClient;
        private string _requestCodeText;
        private int _remainingSeconds;
    }
}
