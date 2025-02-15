using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class DraftConfigViewModel : ObservableObject {
        private string _draftName;
        public string DraftName {
            get { return _draftName; }
            set {
                _draftName = value;
                OnPropertyChanged();
                IsNameOk = !string.IsNullOrEmpty(value) && value.Length <= MaxLength && NameRegex().IsMatch(value);
                IsNextEnable = IsNameOk && IsFolderPathOk;
            }
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        private string _storageFolderPath;
        public string StorageFolderPath {
            get { return _storageFolderPath; }
            set {
                _storageFolderPath = value;
                OnPropertyChanged();
                if (IsValidFolderPath(value)) {
                    DeployNewDraft_Desc = LanguageUtil.GetI18n(Constants.I18n.Project_DeployNewDraft_Desc) + " " + value;
                    IsFolderPathOk = true;
                }
                else {
                    DeployNewDraft_Desc = string.Empty;
                    IsFolderPathOk = false;
                }
                IsNextEnable = IsNameOk && IsFolderPathOk;
            }
        }

        private bool _isFolderPathOk;
        public bool IsFolderPathOk {
            get { return _isFolderPathOk; }
            set { _isFolderPathOk = value; OnPropertyChanged(); }
        }

        private bool _isNextEnable;
        public bool IsNextEnable {
            get { return _isNextEnable; }
            set { _isNextEnable = value; OnPropertyChanged(); }
        }

        public string Project_DeployNewDraft { get; set; }
        public string Project_NewDraftName { get; set; }
        public string Project_NewDraftName_Placeholder { get; set; }
        public string Project_NewDraftName_InvalidTip { get; set; }
        public string Project_NewDraftPosition { get; set; }
        public string Project_NewDraftPosition_BrowserFolder_Tooltip { get; set; }
        public string Project_NewDraftPosition_InvalidTip { get; set; }
        public string Project_DeployNewDraft_PreviousStep { get; set; }
        public string Project_DeployNewDraft_Create { get; set; }

        private string _deployNewDraft_Desc;
        public string DeployNewDraft_Desc {
            get { return _deployNewDraft_Desc; }
            set { _deployNewDraft_Desc = value; OnPropertyChanged(); }
        }

        public DraftConfigViewModel() {
            InitText();
            InitContent();
        }

        private void InitContent() {
            DraftName = "New_Draft";
            StorageFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            IsNextEnable = true;
        }

        private void InitText() {
            Project_DeployNewDraft = LanguageUtil.GetI18n(Constants.I18n.Project_DeployNewDraft);
            Project_NewDraftName = LanguageUtil.GetI18n(Constants.I18n.Project_NewDraftName);
            Project_NewDraftName_Placeholder = LanguageUtil.GetI18n(Constants.I18n.Project_NewDraftName_Placeholder);
            Project_NewDraftName_InvalidTip = LanguageUtil.GetI18n(Constants.I18n.Project_NewDraftName_InvalidTip);
            Project_NewDraftPosition = LanguageUtil.GetI18n(Constants.I18n.Project_NewDraftPosition);
            Project_NewDraftPosition_BrowserFolder_Tooltip = LanguageUtil.GetI18n(Constants.I18n.Project_NewDraftPosition_BrowserFolder_Tooltip);
            Project_NewDraftPosition_InvalidTip = LanguageUtil.GetI18n(Constants.I18n.Project_NewDraftPosition_InvalidTip);
            Project_DeployNewDraft_PreviousStep = LanguageUtil.GetI18n(Constants.I18n.Project_DeployNewDraft_PreviousStep);
            Project_DeployNewDraft_Create = LanguageUtil.GetI18n(Constants.I18n.Project_DeployNewDraft_Create);
        }

        internal async void ChangeFolder(nint hwnd) {
            var storageFolder = await WindowsStoragePickers.PickFolderAsync(hwnd);
            if (storageFolder == null) return;

            StorageFolderPath = storageFolder.Path;
        }

        internal async Task CreateNewVpdAsync() {
            try {

            }
            catch (Exception) {

                throw;
            }
        }

        public static bool IsValidFolderPath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            // 检查路径长度
            if (path.Length < MinLength || path.Length > MaxLength) {
                return false;
            }

            // 快速检查路径的基本格式
            if (!path.StartsWith(@"\\") && (path.Length < 3 || path[1] != ':' || path[2] != '\\')) {
                return false;
            }

            // 检查每个字符是否都在允许的字符集中
            foreach (char c in path) {
                if (!ValidChars.Contains(c)) {
                    return false;
                }
            }

            return true;
        }

        // 定义允许的字符集合
        private static readonly char[] ValidChars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .!@#$%^&()[]{}+=-_\\/:"
            .Concat(Enumerable.Range(0x4e00, 0x9fa5 - 0x4e00 + 1).Select(c => (char)c)).ToArray();

        // 定义路径长度限制
        private const int MinLength = 3; // 最小长度，例如 "C:\"
        private const int MaxLength = 260; // Windows传统路径的最大长度限制

        [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled)]
        private static partial Regex NameRegex();

        internal bool CreateNewDir() {
            string storagePath = Path.Combine(this.StorageFolderPath, this.DraftName);
            try {
                Directory.CreateDirectory(storagePath);
            }
            catch (Exception) {
                return false;
            }

            return true;
        }
    }
}
