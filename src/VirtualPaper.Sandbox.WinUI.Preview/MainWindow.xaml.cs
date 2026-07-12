using System;
using System.Collections.Generic;
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
                _openedWindows.Remove(w);
                RefreshList();
            };
            _openedWindows.Add(w);
            w.Activate();
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
