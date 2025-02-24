using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class DraftConfigViewModel : ObservableObject {
        private string _draftName;
        public string DraftName {
            get { return _draftName; }
            set {
                _draftName = value;
                IsNameOk = ComplianceUtil.IsValidName(value);
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
                IsFolderPathOk = ComplianceUtil.IsValidFolderPath(value);
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
            set { _isNextEnable = value; UpdateDeployNewDraftDesc(value); _configSpace.SetNextStepBtnEnable(value); }
        }

        public string Project_DeployNewDraft { get; set; }
        public string Project_NewDraftName { get; set; }
        public string Project_NewDraftName_Placeholder { get; set; }
        public string Project_NewName_InvalidTip { get; set; }
        public string Project_NewDraftPosition { get; set; }
        public string Project_NewDraftPosition_BrowserFolder_Tooltip { get; set; }
        public string Project_NewPosition_InvalidTip { get; set; }
        public string Project_DeployNewDraft_PreviousStep { get; set; }
        public string Project_DeployNewDraft_Create { get; set; }

        private string _deployNewDraft_Desc;
        public string DeployNewDraft_Desc {
            get { return _deployNewDraft_Desc; }
            set { _deployNewDraft_Desc = value; OnPropertyChanged(); }
        }

        public DraftConfigViewModel() {
            InitText();
        }

        internal void InitContent() {
            DraftName = "New_Draft";
            this.StorageFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);            
        }

        private void InitText() {
            Project_DeployNewDraft = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft));
            Project_NewDraftName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewDraftName));
            Project_NewDraftName_Placeholder = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewDraftName_Placeholder));
            Project_NewName_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewName_InvalidTip));
            Project_NewDraftPosition = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewDraftPosition));
            Project_NewDraftPosition_BrowserFolder_Tooltip = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewDraftPosition_BrowserFolder_Tooltip));
            Project_NewPosition_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewPosition_InvalidTip));
            Project_DeployNewDraft_PreviousStep = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_PreviousStep));
            Project_DeployNewDraft_Create = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_Create));
        }

        internal void InitConfigSpace() {
            _configSpace.SetPreviousStepBtnText(Project_DeployNewDraft_PreviousStep);
            _configSpace.SetNextStepBtnText(Project_DeployNewDraft_Create);
            _configSpace.SetBtnVisible(true);
            _configSpace.BindingPreviousBtnAction(PreviousStepBtnAction);
            _configSpace.BindingNextBtnAction(CreateVpdBtnAction);
        }

        private void PreviousStepBtnAction(object sender, RoutedEventArgs e) {
            _configSpace.ChangePanelState(DraftPanelState.ProjectConfig);
        }

        private async void CreateVpdBtnAction(object sender, RoutedEventArgs e) {
            string storageFolder = await CreateNewVpdAsync();
            if (storageFolder == string.Empty) return;
            _configSpace.ChangePanelState(DraftPanelState.WorkSpace, storageFolder);
        }

        private void UpdateDeployNewDraftDesc(bool value) {
            if (value) {
                DeployNewDraft_Desc = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_Desc)) + " " + Path.Combine(StorageFolderPath, DraftName);
            }
            else {
                DeployNewDraft_Desc = string.Empty;
            }
        }

        internal async void ChangeFolder() {
            var storageFolder = await WindowsStoragePickers.PickFolderAsync(_configSpace.GetWindowHandle());
            if (storageFolder == null) return;

            this.StorageFolderPath = storageFolder.Path;
        }
       
        internal async Task<string> CreateNewVpdAsync() {
            CancellationTokenSource ctsCreate = new();
            string storageFolder = Path.Combine(this.StorageFolderPath, DraftName);
            try {
                _configSpace.GetNotify().Loading(true, false, [ctsCreate]);

                if (Directory.Exists(storageFolder)) {
                    _configSpace.GetNotify().ShowMsg(true, nameof(Constants.I18n.DirExsits), InfoBarType.Error);
                    return string.Empty;
                }

                Directory.CreateDirectory(storageFolder);
                DraftMetadata dm = new(DraftName, Assembly.GetEntryAssembly().GetName().Version, _configSpace.GetParam() as List<ProjectMetadata> ?? []);
                await dm.WriteDataAsync(storageFolder);
            }
            catch (Exception ex) {
                Directory.Delete(storageFolder, true);
                _configSpace.GetNotify().ShowExp(ex);
                _configSpace.Log(LogType.Error, ex);
                return string.Empty;
            }
            finally {
                _configSpace.GetNotify().Loaded([ctsCreate]);
            }

            return storageFolder;
        }

        internal IConfigSpace _configSpace;
    }
}
