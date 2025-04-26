using System;
using System.IO;
using VirtualPaper.Common;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AccountPanel.ViewModels {
    partial class UserInfoEditViewModel : ObservableObject {
        public string SignPlaceholderText { get; set; }
        public string Account_AvatarDesc { get; set; }
        public string Account_ChangeAvatar { get; set; }
        public string Account_PersonalInfo { get; set; }
        public string Account_Username { get; set; }
        public string Account_Sign { get; set; }
        public string Account_UserAccount { get; set; }
        public string Account_Email { get; set; }
        public string Account_Username_InvalidTip { get; set; }
        public string Account_Sign_InvalidTip { get; set; }

        private UserInfo _user;
        public UserInfo User {
            get { return _user; }
            set { _user = value; OnPropertyChanged(); }
        }

        private bool _isUsernameOk = true;
        public bool IsUsernameOk {
            get { return _isUsernameOk; }
            set { _isUsernameOk = value; OnPropertyChanged(); }
        }

        private bool _isSignOk = true;
        public bool IsSignOk {
            get { return _isSignOk; }
            set { _isSignOk = value; OnPropertyChanged(); }
        }

        public UserInfoEditViewModel(UserInfo user) {
            User = user;
            InitText();
        }

        private void InitText() {
            SignPlaceholderText = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_DefaultSign));
            Account_AvatarDesc = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_AvatarDesc));
            Account_ChangeAvatar = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_ChangeAvatar));
            Account_PersonalInfo = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_PersonalInfo));
            Account_Username = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Username));
            Account_Sign = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Sign));
            Account_UserAccount = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_UserAccount));
            Account_Email = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Email));
            Account_Username_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Username_InvalidTip));
            Account_Sign_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Account_Sign_InvalidTip));
        }

        internal void TrySetAvatar(string avatarPath) {
            if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath)) {
                Account.Instance.GetDialog().ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Text_FileNotAccess),
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_Prompt),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm));
                return;
            }

            long maxSizeInBytes = 5 * 1024 * 1024; // 5MB
            FileInfo fileInfo = new(avatarPath);
            if (fileInfo.Length > maxSizeInBytes) {
                Account.Instance.GetDialog().ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Text_FileSizeIllegal_5MB),
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_Prompt),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm));
                return;
            }

            try {
                User.Avatar = File.ReadAllBytes(avatarPath);
            }
            catch (Exception ex) {
                User.Avatar = null;
                Account.Instance.GetDialog().ShowDialogAsync(
                    LanguageUtil.GetI18n(Constants.I18n.Text_FileNotAccess),
                    LanguageUtil.GetI18n(Constants.I18n.Dialog_Title_Prompt),
                    LanguageUtil.GetI18n(Constants.I18n.Text_Confirm));
            }
        }
    }
}
