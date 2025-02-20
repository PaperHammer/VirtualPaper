using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common.Utils.DI;
using VirtualPaper.DraftPanel.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WorkSpace : Page {
        //private ObservableCollection<TabViewItem> items = [];
        
        public WorkSpace() {
            this.InitializeComponent();

            //items.Add(new TabViewItem { Header = "1111文件", Content = new Canvas { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) } });
            //items.Add(new TabViewItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "1312323123xxx1x2x11221x", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "22ww23122文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem  { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            //items.Add(new TabViewItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });

            ////SelectableItems.ItemsSource = items;
            //TabViewControl.TabItemsSource = items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._draftPanel == null) {
                this._draftPanel = e.Parameter as IDraftPanelBridge;

                _viewModel = ObjectProvider.GetRequiredService<WorkSpaceViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
                _viewModel._draftPanel = this._draftPanel;
                this.DataContext = _viewModel;
            }
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args) {
            _viewModel.AddDraftItemAsync();
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) {

        }

        //private void Button_Click(object sender, RoutedEventArgs e) {
        //    ((FrameworkElement)e.OriginalSource).DataContext = null;
        //    items.Remove(e.OriginalSource as TabIDraftItem);
        //}

        private WorkSpaceViewModel _viewModel;
        private IDraftPanelBridge _draftPanel;
    }
}
