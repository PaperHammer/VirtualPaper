using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common.Utils.Bridge;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using Microsoft.UI.Xaml.Navigation;
using VirtualPaper.Common.Utils.DI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WorkSpace : Page {
        private ObservableCollection<TabIDraftItem> items = [];
        
        public WorkSpace() {
            this.InitializeComponent();

            items.Add(new TabIDraftItem { Header = "1111文件", Content = new Canvas { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "1312323123xxx1x2x11221x", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "22ww23122文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new TabIDraftItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });

            //SelectableItems.ItemsSource = items;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (this._draftPanel == null) {
                this._draftPanel = e.Parameter as IDraftPanelBridge;

                _viewModel = ObjectProvider.GetRequiredService<WorkSpaceViewModel>(ObjectLifetime.Transient, ObjectLifetime.Singleton);
                this.DataContext = _viewModel;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            ((FrameworkElement)e.OriginalSource).DataContext = null;
            items.Remove(e.OriginalSource as TabIDraftItem);
        }

        private WorkSpaceViewModel _viewModel;
        private IDraftPanelBridge _draftPanel;
    }
}
