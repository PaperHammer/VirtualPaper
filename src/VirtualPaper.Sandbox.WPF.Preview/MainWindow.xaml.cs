using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using VirtualPaper.lang;
using VirtualPaper.Views;
using Wpf.Ui.Appearance;

namespace VirtualPaper.Sandbox.WPF.Preview {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
            WindowList.ItemsSource = _openedWindows;
            Closed += (_, _) => CloseAllTrackedWindows();
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ThemeCombo.SelectedItem is ComboBoxItem item) {
                var theme = item.Content?.ToString() == "Light"
                    ? ApplicationTheme.Light
                    : ApplicationTheme.Dark;
                ApplicationThemeManager.Apply(theme, updateAccent: false);
            }
        }

        private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (LangCombo.SelectedItem is ComboBoxItem item) {
                var lang = item.Content?.ToString() ?? "zh-CN";
                LanguageManager.Instance.ChangeLanguage(new CultureInfo(lang));
            }
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
            WindowList.ItemsSource = null;
            WindowList.ItemsSource = _openedWindows;
        }

        private void CloseAllTrackedWindows() {
            foreach (var w in _openedWindows.ToList()) {
                w.Close();
            }
            _openedWindows.Clear();
        }

        private readonly ObservableCollection<Window> _openedWindows = [];
    }
}
