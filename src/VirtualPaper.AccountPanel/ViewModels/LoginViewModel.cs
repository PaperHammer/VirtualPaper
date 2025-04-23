using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class LoginViewModel : ObservableObject {
        private string _email;
        public string Email {
            get { return _email; }
            set { _email = value; IsEmailOk = ComplianceUtil.IsValidEmail(value); }
        }

        private string _pwd;
        public string Pwd {
            get { return _pwd; }
            set { _pwd = value; IsPwdOk = ComplianceUtil.IsValidPwd(value); }
        }

        private bool _isEmailOk;
        public bool IsEmailOk {
            get { return _isEmailOk; }
            set { _isEmailOk = value; IsOk = _isEmailOk && _isPwdOk; OnPropertyChanged(); }
        }

        private bool _isPwdOk;
        public bool IsPwdOk {
            get { return _isPwdOk; }
            set { _isPwdOk = value; IsOk = _isEmailOk && _isPwdOk; OnPropertyChanged(); }
        }

        private bool _isOk;
        public bool IsOk {
            get { return _isOk; }
            set { _isOk = value; OnPropertyChanged(); }
        }

        public string Account_Email_InvalidTip { get; private set; }
        public string Account_Pwd_InvalidTip { get; private set; }
        public string Account_LoginWithAccount { get; private set; }
        public string Account_EmailText { get; private set; }
        public string Account_EmailTextWithColon { get; private set; }
        public string Account_PwdText { get; private set; }
        public string Account_PwdTextWithColon { get; private set; }
        public string Account_LoginText { get; private set; }
        public string Account_RegisterText { get; private set; }

        public LoginViewModel(IAccountClient accountClient) {
            _accountClient = accountClient;
            InitText();
        }

        private void InitText() {
            Account_Email_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Email_InvalidTip));
            Account_Pwd_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Pwd_InvalidTip));
            Account_LoginWithAccount = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_LoginWithAccount));
            Account_EmailText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_EmailText));
            Account_EmailTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_EmailTextWithColon));
            Account_PwdText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_PwdText));
            Account_PwdTextWithColon = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_PwdTextWithColon));
            Account_LoginText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_LoginText));
            Account_RegisterText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_RegisterText));
        }

        internal async Task<bool> LoginAsync() {
            Account.Instance.GetNotify().Loading(false, false);
            var response = await _accountClient.LoginAsync(Email, Pwd);
            if (!response.Success) {
                Account.Instance.GetNotify().ShowMsg(
                    true,
                    response.Message,
                    InfoBarType.Error,
                    isAllowDuplication: false);
            }
            Account.Instance.GetNotify().Loaded();

            return response.Success;
        }

        private readonly IAccountClient _accountClient;
    }
}
