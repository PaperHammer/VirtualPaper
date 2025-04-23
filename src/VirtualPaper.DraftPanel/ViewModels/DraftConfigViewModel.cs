using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.Views;
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
            _configSpace.ChangePanelState(DraftPanelState.ProjectConfig, null);
        }

        private async void CreateVpdBtnAction(object sender, RoutedEventArgs e) {
            string filePath = await CreateNewAsync(); // .vpd filePath
            if (filePath == string.Empty) return;
            _configSpace.ChangePanelState(DraftPanelState.WorkSpace, new ToWorkSpace([filePath]));
        }

        private void UpdateDeployNewDraftDesc(bool value) {
            DeployNewDraft_Desc = value ?
                LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_Desc)) + " " + Path.Combine(StorageFolderPath, DraftName)
                : string.Empty;
        }

        internal async void ChangeFolder() {
            var storageFolder = await WindowsStoragePickers.PickFolderAsync(_configSpace.GetWindowHandle());
            if (storageFolder == null) return;

            this.StorageFolderPath = storageFolder.Path;
        }

        /// <summary>
        /// 创建项目
        /// </summary>
        /// <returns>创建的根数据文件路径</returns>
        /// <remarks>
        /// 执行完成后，生成的目录结构如下：
        ///
        /// <code>
        /// 
        /// {draftFolder}
        /// - {draftName}.vpd   根数据文件 (.vpd)
        /// - {SharedData.ProjName}  默认项目的目录
        ///         
        /// </code>
        /// 
        /// </remarks>
        internal async Task<string> CreateNewAsync() {
            Draft.Instance.GetNotify().Loading(false, false);
            string draftFolder = Path.Combine(StorageFolderPath, DraftName);
            try {
                #region 创建 vpd 目录
                if (Directory.Exists(draftFolder)) {
                    Draft.Instance.GetNotify().ShowMsg(true, nameof(Constants.I18n.DirExsits), InfoBarType.Error);
                    return string.Empty;
                }
                Directory.CreateDirectory(draftFolder);
                #endregion

                #region 创建 .vpd 文件
                var inputData = _configSpace.GetSharedData() as ToDraftConfig;
                DraftMetadata draftdata = new(DraftName, inputData);
                await draftdata.SaveAsync(draftFolder);
                #endregion

                #region 创建 vproj 目录
                string projFolder = Path.Combine(draftFolder, inputData.ProjName);
                Directory.CreateDirectory(projFolder);
                #endregion

                //#region 创建 .vproj 文件
                //ProjectMetadata projdata = new(inputData.ProjName, inputData.ProjType);
                //await projdata.SaveBasicAsync(projFolder);
                //#endregion
            }
            catch (Exception ex) {
                Directory.Delete(draftFolder, true);
                Draft.Instance.GetNotify().ShowExp(ex);
                Draft.Instance.Log(LogType.Error, ex);
                return string.Empty;
            }
            finally {
                Draft.Instance.GetNotify().Loaded();
            }

            return Path.Combine(draftFolder, DraftName + FileExtension.FE_Design); // .vpd filePath
        }

        internal ConfigSpace _configSpace;
    }
}
