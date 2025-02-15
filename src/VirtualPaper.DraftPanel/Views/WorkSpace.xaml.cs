using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.DraftPanel.Views {
    public class PanelItem {
        public string Header { get; set; }
        public UIElement Content { get; set; }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WorkSpace : Page {
        private ObservableCollection<PanelItem> items = [];
        public WorkSpace() {
            this.InitializeComponent();
            items.Add(new PanelItem { Header = "1111文件", Content = new Canvas { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "1312323123xxx1x2x11221x", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "22ww23122文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            items.Add(new PanelItem { Header = "2222文件", Content = new TextBlock { Text = "This is Tab 2 content.", Margin = new Thickness(20) } });
            SelectableItems.ItemsSource = items;
        }

        public void SelectTab(PanelItem tabItem) {
            ContentFrame.Content = tabItem.Content;
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            ((FrameworkElement)e.OriginalSource).DataContext = null;
            items.Remove(e.OriginalSource as PanelItem);
        }
    }
}
