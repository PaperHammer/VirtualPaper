using System.Collections.Generic;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.Views;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class ProjectConfigViewModel : ObservableObject {
        public List<ProjectTemplate> AvailableTemplates { get; set; } = [];

        private string _projectName;
        public string ProjectName {
            get { return _projectName; }
            set {
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

        public string Project_NewProjectName { get; set; }
        public string Project_NewProjectName_Placeholder { get; set; }
        public string Project_NewName_InvalidTip { get; set; }
        public string Project_TemplateConfig { get; set; }
        public string Project_SearchTemplate { get; set; }
        public string Project_DeployNewDraft_PreviousStep { get; set; }
        public string Project_DeployNewDraft_NextStep { get; set; }

        public ProjectConfigViewModel() {
            InitText();
        }

        internal void InitContent() {
            ProjectName = "New_Project";
            AvailableTemplates = [
                new() {
                    ItemImageKey = "ui_components/project-create-static-img.png",
                    DescImageKey = "ui_components/project-create-static-image-desc.png",
                    Name = "静态图像",
                    Desc = "静态图像",
                    Type = ProjectType.PImage,
                },
                new() {
                    ItemImageKey = "ui_components/project-create-static-img.png",
                    DescImageKey = "ui_components/project-create-static-image-desc.png",
                    Name = "静态图像2",
                    Desc = "静态图像2",
                    Type = ProjectType.PImage,
                },
            ];

            _availableTemplates = [.. AvailableTemplates];
        }

        internal void InitConfigSpace() {
            _configSpace.SetPreviousStepBtnText(Project_DeployNewDraft_PreviousStep);
            _configSpace.SetNextStepBtnText(Project_DeployNewDraft_NextStep);
            _configSpace.SetBtnVisible(true);
            _configSpace.BindingPreviousBtnAction(PreviousStepBtnAction);
            _configSpace.BindingNextBtnAction(NextStepBtnAction);
        }

        private void InitText() {
            Project_NewProjectName = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewProjectName));
            Project_NewProjectName_Placeholder = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewProjectName_Placeholder));
            Project_NewName_InvalidTip = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_NewName_InvalidTip));
            Project_TemplateConfig = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_TemplateConfig));
            Project_SearchTemplate = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_SearchTemplate));
            Project_DeployNewDraft_PreviousStep = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_PreviousStep));
            Project_DeployNewDraft_NextStep = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_DeployNewDraft_NextStep));
        }

        private void PreviousStepBtnAction(object sender, RoutedEventArgs e) {
            _configSpace.ChangePanelState(DraftPanelState.GetStart, null);
        }

        private void NextStepBtnAction(object sender, RoutedEventArgs e) {
            _configSpace.ChangePanelState(DraftPanelState.DraftConfig, new ToDraftConfig(ProjectName, SelectedTemplate.Type));
        }

        internal IEnumerable<ProjectTemplate> _availableTemplates;
        internal ConfigSpace _configSpace;
    }
}
