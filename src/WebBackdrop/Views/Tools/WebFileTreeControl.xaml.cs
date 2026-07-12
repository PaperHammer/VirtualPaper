using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Workloads.Creation.WebBackdrop.Views.Tools {
    public sealed partial class WebFileTreeControl : UserControl {
        public event EventHandler<string>? FileOpenRequested;
        public event EventHandler<string>? NewFileRequested;

        public string ProjectName {
            get => (string)GetValue(ProjectNameProperty);
            set => SetValue(ProjectNameProperty, value);
        }
        public static readonly DependencyProperty ProjectNameProperty =
            DependencyProperty.Register(nameof(ProjectName), typeof(string), typeof(WebFileTreeControl), new PropertyMetadata(string.Empty));

        public List<WebFileItem> FileItems {
            get => (List<WebFileItem>)GetValue(FileItemsProperty);
            set => SetValue(FileItemsProperty, value);
        }
        public static readonly DependencyProperty FileItemsProperty =
            DependencyProperty.Register(nameof(FileItems), typeof(List<WebFileItem>), typeof(WebFileTreeControl), new PropertyMetadata(null));

        public WebFileTreeControl() {
            InitializeComponent();
        }

        public void Refresh(string projectFolder) {
            if (!Directory.Exists(projectFolder)) return;

            var items = Directory.GetFiles(projectFolder)
                .Select(f => new WebFileItem(f))
                .ToList();
            FileItems = items;
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.FirstOrDefault() is WebFileItem item)
                FileOpenRequested?.Invoke(this, item.FilePath);
        }

        private void NewFile_Click(object sender, RoutedEventArgs e) {
            NewFileRequested?.Invoke(this, EventArgs.Empty.ToString()!);
        }
    }

    public class WebFileItem {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public string FileGlyph => Path.GetExtension(FilePath).ToLowerInvariant() switch {
            ".html" or ".htm" => "\uE8A1",
            ".css"            => "\uE790",
            ".js"             => "\uE943",
            ".json"           => "\uE8F4",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" => "\uEB9F",
            _                 => "\uE8A5",
        };

        public WebFileItem(string filePath) {
            FilePath = filePath;
        }
    }
}
