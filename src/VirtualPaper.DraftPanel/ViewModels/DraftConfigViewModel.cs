using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.DraftPanel.ViewModels {
    public partial class DraftConfigViewModel : ObservableObject {
        public Action? CardUIStateChanged { get; set; }
        public ObservableCollection<ProjectTemplate> AvailableTemplates { get; set; } = [];

        private string? _projectName;
        public string? ProjectName {
            get { return _projectName; }
            set {
                if (_projectName == value) return;

                _projectName = value;
                OnPropertyChanged();
                IsNameOk = ComplianceUtil.IsValidName(value);
                RefreshNextState();
                RefreshProjectCreatePath();
            }
        }

        private string? _projectLocation;
        public string? ProjectLocation {
            get => _projectLocation;
            set {
                if (_projectLocation == value) return;
                _projectLocation = value;
                OnPropertyChanged();
                IsLocationOk = ComplianceUtil.IsValidFolderPath(value);
                RefreshNextState();
                RefreshProjectCreatePath();
            }
        }

        private bool _isLocationOk;
        public bool IsLocationOk {
            get { return _isLocationOk; }
            set { _isLocationOk = value; OnPropertyChanged(); }
        }

        private string _projectCreatePathText = string.Empty;
        public string ProjectCreatePathText {
            get { return _projectCreatePathText; }
            set { if (_projectCreatePathText == value) return; _projectCreatePathText = value; OnPropertyChanged(); }
        }

        public bool IsProjectCreatePathVisible => IsNameOk && IsLocationOk;

        public bool IsWebTemplateSelected => SelectedTemplate?.Type == ProjectType.P_WebBackdrop;

        public ICommand BrowseProjectLocationCommand { get; }

        public DraftConfigViewModel() {
            BrowseProjectLocationCommand = new RelayCommand(async () => await BrowseProjectLocationAsync());
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        private bool _isNextEnable;
        public bool IsNextEnable {
            get { return _isNextEnable; }
            set { _isNextEnable = value; CardUIStateChanged?.Invoke(); }
        }

        private ProjectTemplate? _selectedTemplate;
        public ProjectTemplate? SelectedTemplate {
            get { return _selectedTemplate; }
            set {
                _selectedTemplate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWebTemplateSelected));
                RefreshNextState();
                RefreshProjectCreatePath();
            }
        }

        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public string NextStepBtnText { get; private set; } = string.Empty;
        public bool BtnVisible { get; private set; } = false;
        public bool IsFromWorkSpaceForAddProj { get; set; }
        public TaskCompletionSource<PreProjectData[]?>? DraftConfigTCS { get; set; }

        internal async Task InitContentAsync() {
            SelectedTemplate = null;

            var ctx = ArcPageContextManager.GetContext<Draft>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        var configData = await JsonSaver.LoadAsync<AvailableDraftTemplate>(_configPath, AvailableDraftTemplateContext.Default);
                        if (configData != null) {
                            CrossThreadInvoker.InvokeOnUIThread(() => {
                                ProjectName = configData.DefaultProjectName!;
                                AvailableTemplates.SetRange(configData.Templates!);
                            });
                        }

                        _availableTemplates = [.. AvailableTemplates];
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<DraftConfigViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ex);
                    }
                });
        }

        public void UpdateCardComponentUI() {
            PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_PreviousStep));
            NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));
            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        private void RefreshNextState() {
            IsNextEnable = IsNameOk
                && SelectedTemplate != null
                && (!IsWebTemplateSelected || IsLocationOk);
        }

        private void RefreshProjectCreatePath() {
            ProjectCreatePathText = IsNameOk && IsLocationOk
                ? Path.Combine(ProjectLocation!, ProjectName!)
                : string.Empty;
            OnPropertyChanged(nameof(IsProjectCreatePathVisible));
        }

        private async Task BrowseProjectLocationAsync() {
            var folder = await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle);
            if (folder == null) return;

            ProjectLocation = folder.Path;
        }

        public Task OnNextStepClickedAsync() {
            if (SelectedTemplate == null || !IsNameOk || (IsWebTemplateSelected && !IsLocationOk)) return Task.CompletedTask;

            var identity = IsWebTemplateSelected
                ? Path.Combine(ProjectLocation!, ProjectName!)
                : ProjectName!;
            var intent = IsWebTemplateSelected
                ? ProjectOpenIntent.CreateAtDirectory
                : ProjectOpenIntent.CreateFromName;
            var preData = new PreProjectData[] { new(identity, SelectedTemplate!.Type, intent) };

            if (IsFromWorkSpaceForAddProj) {
                DraftConfigTCS?.TrySetResult(preData);
            }
            else {
                _navigateComponent?.GetPaylaod()?.Set(NaviPayloadKey.Project, preData);
                _navigateComponent?.NavigateByState(DraftPanelState.WorkSpace);
            }

            return Task.CompletedTask;
        }

        public Task OnPreviousStepClickedAsync() {
            _navigateComponent?.NavigateByState(DraftPanelState.GetStart);

            return Task.CompletedTask;
        }

        #region filter
        public void ApplyFilter(string keyword) {
            Filter(keyword);
        }

        private void Filter(string keyword) {
            var filtered = _availableTemplates?.Where(template =>
                template.Name != null && template.Name.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)
            );
            if (filtered == null) return;
            Remove_NonMatching(filtered);
            AddBack_Procs(filtered);
        }

        private void Remove_NonMatching(IEnumerable<ProjectTemplate> templates) {
            for (int i = AvailableTemplates.Count - 1; i >= 0; i--) {
                var item = AvailableTemplates[i];
                if (!templates.Contains(item)) {
                    AvailableTemplates.Remove(item);
                }
            }
        }

        private void AddBack_Procs(IEnumerable<ProjectTemplate> templates) {
            foreach (var item in templates) {
                if (!AvailableTemplates.Contains(item)) {
                    AvailableTemplates.Add(item);
                }
            }
        }
        #endregion

        private IEnumerable<ProjectTemplate>? _availableTemplates;
        internal INavigateComponent _navigateComponent = null!;
        private readonly string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "DraftPanelConfigs",
            "available_draft_template.json"
        );
    }
}
