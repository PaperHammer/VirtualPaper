using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class DraftConfigViewModel : ObservableObject {
        public ObservableCollection<ProjectTemplate> AvailableTemplates { get; set; } = [];

        private string _projectName;
        public string ProjectName {
            get { return _projectName; }
            set {
                if (_projectName == value) return;

                _projectName = value;
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
            set { _isNextEnable = value; _configSpace.SetNextStepBtnEnable(value); }
        }

        private ProjectTemplate _selectedTemplate;
        public ProjectTemplate SelectedTemplate {
            get { return _selectedTemplate; }
            set { _selectedTemplate = value; OnPropertyChanged(); IsNextEnable = IsNameOk && value != null; }
        }

        public string Project_DeployNewDraft_PreviousStep { get; set; }
        public string Project_DeployNewDraft_NextStep { get; set; }

        public DraftConfigViewModel() {
            InitText();
        }

        internal async Task InitContentAsync() {
            var ctx = ArcPageContextManager.GetContext<Draft>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(
                operation: async token => {
                    try {
                        var configData = await JsonSaver.LoadAsync<AvailableDraftTemplate>(_configPath, AvailableDraftTemplateContext.Default);
                        if (configData != null) {
                            ProjectName = configData.DefaultProjectName!;
                            AvailableTemplates.SetRange(configData.Templates!);
                        }

                        _availableTemplates = [.. AvailableTemplates];
                    }
                    catch (Exception ex) {
                        ArcLog.GetLogger<DraftConfigViewModel>().Error(ex);
                        GlobalMessageUtil.ShowException(ArcWindowManager.GetArcWindow(new(ArcWindowKey.Main)), ex);
                    }
                });
        }

        internal void InitConfigSpace() {
            _configSpace.SetPreviousStepBtnText(Project_DeployNewDraft_PreviousStep);
            _configSpace.SetNextStepBtnText(Project_DeployNewDraft_NextStep);
            _configSpace.SetBtnVisible(true);
            _configSpace.BindingPreviousBtnAction(PreviousStepBtnAction);
            _configSpace.BindingNextBtnAction(NextStepBtnAction);
        }

        private void InitText() {
            Project_DeployNewDraft_PreviousStep = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_PreviousStep));
            Project_DeployNewDraft_NextStep = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_NextStep));
        }

        private void PreviousStepBtnAction(object sender, RoutedEventArgs e) {
            _configSpace.ChangePanelState(DraftPanelState.GetStart, null);
        }

        private void NextStepBtnAction(object sender, RoutedEventArgs e) {
            _configSpace.ChangePanelState(DraftPanelState.WorkSpace, new ToWorkSpace([ProjectName], ProjectType.PImage));
        }

        internal IEnumerable<ProjectTemplate> _availableTemplates;
        internal ConfigSpace _configSpace;
        private readonly string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "DraftPanelConfigs",
            "available_draft_template.json"
        );
    }
}
