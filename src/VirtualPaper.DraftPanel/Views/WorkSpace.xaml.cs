using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.Model.NavParam;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WorkSpace : Page {
        internal static DraftMetadata DraftMetadataRuntime { get; set; }
        internal static ObservableCollection<ProjectMetadata> ProjectMetadatasRuntime { get; private set; } = [];

        public WorkSpace() {
            this.InitializeComponent();

            ProjectMetadatasRuntime.CollectionChanged += ProjectMetadatasRuntime_CollectionChanged;
        }

        private void ProjectMetadatasRuntime_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.OldItems != null && e.OldItems.Count > 0) {
                string projName = (e.OldItems[0] as ProjectMetadata).Name;
                int hash = IdentifyUtil.ComputeHash(projName);
                DraftMetadataRuntime.ProjectTags.RemoveAll(x => x.Hash == hash);
            }
            if (e.NewItems != null && e.NewItems.Count > 0) {
                string projName = (e.NewItems[0] as ProjectMetadata).Name;
                DraftMetadataRuntime.ProjectTags.Add(new ProjectTag(projName));
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            _viewModel = ObjectProvider.GetRequiredService<WorkSpaceViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
            _draftPanelBridge = e.Parameter as IDraftPanelBridge;
            this.DataContext = _viewModel;
        }

        private void TabViewControl_Loaded(object sender, RoutedEventArgs e) {
            var data = _draftPanelBridge.GetSharedData() as ToWorkSpace;
            _viewModel.InitTabViewItems(data);
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            _viewModel.AddDraftItem();
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {

        }

        private async void MFI_Exit_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.ExitAsync();
        }

        private async void MFI_Save_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.SaveAsync();
        }

        private async void MFI_SaveAll_Clicked(object sender, RoutedEventArgs e) {
            await _viewModel.SaveAllAsync();
        }

        private WorkSpaceViewModel _viewModel;
        private IDraftPanelBridge _draftPanelBridge;
    }
}
