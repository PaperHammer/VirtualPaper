using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.Sandbox.WinUI.Preview {
    public sealed partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
            WindowList.ItemsSource = _openedWindows;
            Closed += (_, _) => CloseAllTrackedWindows();
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ThemeCombo.SelectedItem is not ComboBoxItem item) return;
            if (Content is FrameworkElement root) {
                root.RequestedTheme = item.Content?.ToString() == "Light"
                    ? ElementTheme.Light
                    : ElementTheme.Dark;
            }
        }

        private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            // Language change handled at app level if needed
        }

        private void OpenWebEditorWindow(object sender, RoutedEventArgs e) {
            var payload = new FrameworkPayload() {
                [NaviPayloadKey.StaticImgFileName] = "test_web_project"
            };
            TrackAndShow(new WebEditorWindow(payload), "WebEditorWindow");
        }

        private void TrackAndShow(Window w, string label) {
            w.Title = string.IsNullOrEmpty(w.Title) ? label : w.Title;
            w.Closed += (_, _) => {
                var item = _openedWindows.FirstOrDefault(x => x.Window == w);
                if (item != null) {
                    _openedWindows.Remove(item);
                }
                RefreshList();
            };
            _openedWindows.Add(new OpenedWindowItem(w, string.IsNullOrEmpty(w.Title) ? label : w.Title));
            w.Activate();
            RefreshList();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) {
            if (WindowList.SelectedItem is OpenedWindowItem item) {
                item.Window.Close();
            }
        }

        private void RefreshList() {
            WindowList.ItemsSource = null;
            WindowList.ItemsSource = _openedWindows;
        }

        private void CloseAllTrackedWindows() {
            foreach (var item in _openedWindows.ToList()) {
                item.Window.Close();
            }
            _openedWindows.Clear();
        }

        private readonly ObservableCollection<OpenedWindowItem> _openedWindows = [];
    }

    public sealed class OpenedWindowItem {
        public OpenedWindowItem(Window window, string title) {
            Window = window;
            Title = title;
        }

        public Window Window { get; }
        public string Title { get; }
    }
}
