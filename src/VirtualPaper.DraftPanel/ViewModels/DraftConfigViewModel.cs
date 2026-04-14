using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
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
                IsNextEnable = IsNameOk && SelectedTemplate != null;
            }
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
            set { _selectedTemplate = value; OnPropertyChanged(); IsNextEnable = IsNameOk && value != null; }
        }

        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public string NextStepBtnText { get; private set; } = string.Empty;
        public bool BtnVisible { get; private set; } = false;
        public bool IsFromWorkSpace_AddProj { get; set; }
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
            if (IsFromWorkSpace_AddProj) {
                PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel));
                NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));
            }
            else {
                PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_PreviousStep));
                NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));
            }
            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        public async Task OnNextStepClickedAsync() {
            var preData = new PreProjectData[] { new(ProjectName!, ProjectType.P_StaticImage) };
            _navigateComponent?.GetPaylaod()?.Set(NaviPayloadKey.Project, preData);

            if (IsFromWorkSpace_AddProj) {
                DraftConfigTCS?.TrySetResult(preData);
            }
            else {
                _navigateComponent?.NavigateByState(DraftPanelState.WorkSpace);
            }
        }

        public async Task OnPreviousStepClickedAsync() {
            if (IsFromWorkSpace_AddProj) {
                DraftConfigTCS?.TrySetResult(null);
            }
            else {
                _navigateComponent?.NavigateByState(DraftPanelState.GetStart);
            }
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
