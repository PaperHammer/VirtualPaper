using System.Collections.Generic;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Windows.ApplicationModel.DataTransfer;

namespace VirtualPaper.DraftPanel.ViewModels {
    public class GetStartViewModel {
        //public List<Startup> Startups { get; private set; } = [];
        public ObservableList<IRecentUsed> RecentUseds { get; private set; } = [];

        public string Project_RecentUsed { get; private set; }
        public string Project_SearchRecentUsed { get; private set; }
        public string Project_StartUp { get; private set; }
        public string Project_ContinueWithoutFile { get; private set; }

        public ICommand? RemoveFromListCommand { get; private set; }
        public ICommand? CopyPathCommand { get; private set; }

        public GetStartViewModel(IUserSettingsClient userSettingsClient) {
            this._userSettingsClient = userSettingsClient;
            InitText();
            InitCollection();
            InitCommand();
        }

        private void InitCommand() {
            RemoveFromListCommand = new RelayCommand<IRecentUsed>(async item => {
                if (item != null) {
                    RecentUseds.Remove(item);
                    _recentUsed.Remove(item);
                    await _userSettingsClient.DeleteRecetUsedAsync(item);
                }
            });

            CopyPathCommand = new RelayCommand<IRecentUsed>(item => {
                if (item?.FilePath != null) {
                    var package = new DataPackage();
                    package.SetText(item.FilePath);
                    Clipboard.SetContent(package);
                }
            });
        }

        private void InitCollection() {
            //Startups = [
            //    new(ConfigSpacePanelType.OpenVpd,
            //        LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenVsd),
            //        LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenVsd_Desc),
            //        VirtualKey.V),
            //    new(ConfigSpacePanelType.OpenFile,
            //        LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenFile),
            //        LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_OpenFile_Desc),
            //        VirtualKey.F),
            //    new(ConfigSpacePanelType.NewVpd,
            //        LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_NewVpd),
            //        LanguageUtil.GetI18n(Constants.I18n.Project_StartUp_NewVpd_Desc),
            //        VirtualKey.N),
            //];
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
