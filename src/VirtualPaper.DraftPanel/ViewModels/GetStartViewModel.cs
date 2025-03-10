using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.UIComponent.Utils;
using Windows.System;

namespace VirtualPaper.DraftPanel.ViewModels {
    public class GetStartViewModel {
        public List<Startup> Startups { get; private set; } = [];
        public ObservableCollection<RecentUsed> RecentUseds { get; private set; } = [];

        public string Project_RecentUsed { get; set; }
        public string Project_SearchRecentUsed { get; set; }
        public string Project_StartUp { get; set; }
        public string Project_ContinueWithoutFile { get; set; }

        public GetStartViewModel() {
            InitText();
            InitCollection();
        }

        private void InitCollection() {
            Startups = [
                new(DraftPanelStartupType.OpenVpd,
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenVsd),
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenVsd_Desc),
                    VirtualKey.V),
                new(DraftPanelStartupType.OpenFile,
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenFile),
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenFile_Desc),
                    VirtualKey.F),
                new(DraftPanelStartupType.NewVpd,
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_NewVpd),
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_NewVpd_Desc),
                    VirtualKey.N),
            ];
            RecentUseds = [
                new(ProjectType.PImage, "aaa某个项目", "ccc路径", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc路径", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                new(ProjectType.PImage, "aaa", "ccc", DateTime.Now.ToString()),
                ];
            _recentUsed = [.. RecentUseds];
        }

        private void InitText() {
            Project_RecentUsed = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_RecentUsed));
            Project_SearchRecentUsed = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_SearchRecentUsed));
            Project_StartUp = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StartUp));
            Project_ContinueWithoutFile = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_ContinueWithoutFile));
        }

        internal List<RecentUsed> _recentUsed = [];
    }
}
