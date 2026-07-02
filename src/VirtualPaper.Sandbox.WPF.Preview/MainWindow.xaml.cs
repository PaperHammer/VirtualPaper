using System.Collections.ObjectModel;
using System.Windows;
using VirtualPaper.Views;

namespace VirtualPaper.Sandbox.WPF.Preview {
    public partial class MainWindow {
        private readonly ObservableCollection<Window> _openedWindows = new();

        public MainWindow() {
            InitializeComponent();
            WindowList.ItemsSource = _openedWindows;
        }

        private void OpenPluginUpdateWindow(object sender, RoutedEventArgs e) {
            TrackAndShow(new PluginUpdateWindow(), "PluginUpdateWindow");
        }

        private void OpenSplashWindow(object sender, RoutedEventArgs e) {
            TrackAndShow(new SplashWindow(), "SplashWindow");
        }

        private void OpenIdentifyWindow(object sender, RoutedEventArgs e) {
            TrackAndShow(new IdentifyWindow(index: 1), "IdentifyWindow");
        }

        private void OpenDebugLog(object sender, RoutedEventArgs e) {
            TrackAndShow(new DebugLog(), "DebugLog");
        }

        private void TrackAndShow(Window w, string label) {
            w.Title = string.IsNullOrEmpty(w.Title) ? label : w.Title;
            w.Closed += (_, _) => _openedWindows.Remove(w);
            _openedWindows.Add(w);
            w.Show();
            RefreshList();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) {
            if (WindowList.SelectedItem is Window w) {
                w.Close();
            }
        }

        private void RefreshList() {
            // trigger UI refresh
            var items = WindowList.Items;
            WindowList.ItemsSource = null;
            WindowList.ItemsSource = _openedWindows;
        }
    }
}
