using System.Collections.Generic;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Windows.System;

namespace VirtualPaper.DraftPanel.ViewModels {
    public class GetStartViewModel {
        public List<Startup> Startups { get; private set; } = [];
        public ObservableList<IRecentUsed> RecentUseds { get; private set; } = [];

        public string Project_RecentUsed { get; set; }
        public string Project_SearchRecentUsed { get; set; }
        public string Project_StartUp { get; set; }
        public string Project_ContinueWithoutFile { get; set; }

        public GetStartViewModel(IUserSettingsClient userSettingsClient) {
            this._userSettingsClient = userSettingsClient;
            InitText();
            InitCollection();            
        }

        private void InitCollection() {
            Startups = [
                new(ConfigSpacePanelType.OpenVpd,
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenVsd),
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenVsd_Desc),
                    VirtualKey.V),
                new(ConfigSpacePanelType.OpenFile,
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenFile),
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenFile_Desc),
                    VirtualKey.F),
                new(ConfigSpacePanelType.NewVpd,
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_NewVpd),
                    LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_NewVpd_Desc),
                    VirtualKey.N),
            ];
            RecentUseds.AddRange(_userSettingsClient.RecentUseds);
            _recentUsed = [.. RecentUseds];
        }

        private void InitText() {
            Project_RecentUsed = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_RecentUsed));
            Project_SearchRecentUsed = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_SearchRecentUsed));
            Project_StartUp = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_StartUp));
            Project_ContinueWithoutFile = LanguageUtil.GetI18n(nameof(Constants.I18n.Project_ContinueWithoutFile));
        }

        internal List<IRecentUsed> _recentUsed = [];
        private readonly IUserSettingsClient _userSettingsClient;
    }
}
