using VirtualPaper.Common;
using VirtualPaper.UI.Utils;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.ViewModels;

namespace VirtualPaper.UI.ViewModels {
    public class MainWindowViewModel {
        //public ObservableList<BackgroundTask> BackgroundTasks { get; set; } = [];
        public string SidebarGallery { get; private set; }
        public string SidebarWpSettings { get; private set; }
        public string SidebarProject { get; private set; }
        public string SidebarAccount { get; private set; }
        public string SidebarAppSettings { get; private set; }

        public MainWindowViewModel() {
            _basicComponentUtil = new();
            _dialog = new();

            InitText();           
        }

        private void InitText() {
            SidebarGallery = LanguageUtil.GetI18n(Constants.I18n.SidebarGallery);
            SidebarWpSettings = LanguageUtil.GetI18n(Constants.I18n.SidebarWpSettings);
            SidebarProject = LanguageUtil.GetI18n(Constants.I18n.SidebarProject);
            SidebarAccount = LanguageUtil.GetI18n(Constants.I18n.SidebarAccount);
            SidebarAppSettings = LanguageUtil.GetI18n(Constants.I18n.SidebarAppSettings);
        }

        internal readonly BasicComponentUtil _basicComponentUtil;
        internal readonly DialogUtil _dialog;
    }
}
