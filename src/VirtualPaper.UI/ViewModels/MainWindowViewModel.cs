using VirtualPaper.Common;
using VirtualPaper.UI.Utils;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UI.ViewModels {
    public partial class MainWindowViewModel {
        //public ObservableList<BackgroundTask> BackgroundTasks { get; set; } = [];
        //public string SidebarGallery { get; private set; }
        public string NavRepository => LanguageUtil.GetI18n(Constants.I18n.SidebarRepository);
        public string NavProject => LanguageUtil.GetI18n(Constants.I18n.SidebarProject);
        //public string SidebarAccount { get; private set; }
        //public string SidebarAppSettings { get; private set; }

        public MainWindowViewModel() {
            _basicComponentUtil = new BasicComponentUtil();
            _dialog = new DialogUtil();
        }

        internal readonly BasicComponentUtil _basicComponentUtil;
        internal readonly DialogUtil _dialog;
    }
}
