using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.Model.Interfaces;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.ViewModels {
    internal partial class ProjectConfigViewModel : ObservableObject {
        public List<ProjectTemplate> AvailableTemplates { get; set; } = [];

        private bool _isNextEnable;
        public bool IsNextEnable {
            get { return _isNextEnable; }
            set { _isNextEnable = value; _configSpace.SetNextStepBtnEnable(value); }
        }

        private ProjectTemplate _selectedTemplate;
        public ProjectTemplate SelectedTemplate {
            get { return _selectedTemplate; }
            set { _selectedTemplate = value; OnPropertyChanged(); IsNextEnable = value != null; }
        }

        public string Project_TemplateConfig { get; set; }
        public string Project_SearchTemplate { get; set; }
        public string Project_DeployNewDraft_PreviousStep { get; set; }
        public string Project_DeployNewDraft_NextStep { get; set; }

        public ProjectConfigViewModel() {
            InitText();
            InitContent();
        }

        private void InitContent() {
            AvailableTemplates = [
                new() {
                    ItemImageKey = "project-create-static-img.png",
                    DescImageKey = "project-create-static-image-desc.png",
                    Name = "静态图像",
                    Desc = "静态图像",
                    Type = ProjectType.PImage,
                },
                new() {
                    ItemImageKey = "project-create-static-img.png",
                    DescImageKey = "project-create-static-image-desc.png",
                    Name = "静态图像2",
                    Desc = "静态图像2",
                    Type = ProjectType.PImage,
                },
            ];

            _availableTemplates = [.. AvailableTemplates];
        }

        private void InitText() {
            Project_TemplateConfig = LanguageUtil.GetI18n(Constants.I18n.Project_TemplateConfig);
            Project_SearchTemplate = LanguageUtil.GetI18n(Constants.I18n.Project_SearchTemplate);
            Project_DeployNewDraft_PreviousStep = LanguageUtil.GetI18n(Constants.I18n.Project_DeployNewDraft_PreviousStep);
            Project_DeployNewDraft_NextStep = LanguageUtil.GetI18n(Constants.I18n.Project_DeployNewDraft_NextStep);
        }

        internal IEnumerable<ProjectTemplate> _availableTemplates;
        internal IConfigSpace _configSpace;
    }
}
