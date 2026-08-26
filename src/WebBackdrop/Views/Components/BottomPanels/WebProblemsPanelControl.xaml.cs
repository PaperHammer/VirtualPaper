using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace Workloads.Creation.WebBackdrop.Views.Components.BottomPanels {
    public sealed partial class WebProblemsPanelControl : UserControl {
        private const int MaxVisibleProblemsPerFile = 100;
        private const int MaxVisibleFileGroups = 999;

        public event EventHandler<ProblemItem>? ProblemRequested;

        public WebProblemsPanelControl() {
            InitializeComponent();
        }

        public ObservableCollection<object> Items { get; } = [];

        public void SetProjectFolder(string projectFolder) {
            _projectFolder = projectFolder;
        }

        public void UpdateProblems(string filePath, IReadOnlyList<MonacoMarker> markers) {
            var summary = CreateProblemSummary(filePath, markers);
            var group = FindGroup(filePath);

            if (summary.TotalCount == 0) {
                if (group != null) {
                    Items.Remove(group);
                }
                _hiddenProblemFiles.Remove(filePath);
                UpdateFileOverflowGroup();
                return;
            }

            if (group != null) {
                if (!group.HasSameItems(summary)) {
                    group.SetItems(summary);
                }
                return;
            }

            if (_hiddenProblemFiles.ContainsKey(filePath)) {
                _hiddenProblemFiles[filePath] = summary;
                UpdateFileOverflowGroup();
                return;
            }

            if (VisibleFileGroupCount >= MaxVisibleFileGroups) {
                _hiddenProblemFiles[filePath] = summary;
                UpdateFileOverflowGroup();
                return;
            }

            InsertGroup(new ProblemFileGroup(filePath, GetRelativeDirectory(filePath), summary));
        }

        public void RemoveFile(string filePath) {
            var group = FindGroup(filePath);
            if (group != null) {
                Items.Remove(group);
            }
            _hiddenProblemFiles.Remove(filePath);
            UpdateFileOverflowGroup();
        }

        public int ProblemCount => Items.OfType<ProblemFileGroup>().Sum(group => group.TotalCount)
            + _hiddenProblemFiles.Values.Sum(summary => summary.TotalCount);
        public int ErrorCount => Items.OfType<ProblemFileGroup>().Sum(group => group.ErrorCount)
            + _hiddenProblemFiles.Values.Sum(summary => summary.ErrorCount);
        public int WarningCount => Items.OfType<ProblemFileGroup>().Sum(group => group.WarningCount)
            + _hiddenProblemFiles.Values.Sum(summary => summary.WarningCount);

        private int VisibleFileGroupCount => Items.OfType<ProblemFileGroup>().Count(group => !group.IsOverflow);

        private ProblemFileGroup? FindGroup(string filePath) {
            return Items.OfType<ProblemFileGroup>()
                .FirstOrDefault(item => !item.IsOverflow && string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        private void InsertGroup(ProblemFileGroup group) {
            var index = 0;
            while (index < Items.Count && Items[index] is ProblemFileGroup current && !current.IsOverflow
                && string.Compare(current.FileName, group.FileName, StringComparison.OrdinalIgnoreCase) <= 0) {
                index++;
            }
            Items.Insert(index, group);
        }

        private void UpdateFileOverflowGroup() {
            if (_hiddenProblemFiles.Count == 0) {
                if (_fileOverflowGroup != null) {
                    Items.Remove(_fileOverflowGroup);
                    _fileOverflowGroup = null;
                }
                return;
            }

            var text = string.Format(LanguageUtil.GetI18n("WebBackdrop_HiddenFiles"), _hiddenProblemFiles.Count);
            if (_fileOverflowGroup == null) {
                _fileOverflowGroup = ProblemFileGroup.CreateOverflow(text);
                Items.Add(_fileOverflowGroup);
                return;
            }

            _fileOverflowGroup.SetOverflowText(text);
            if (!Items.Contains(_fileOverflowGroup)) {
                Items.Add(_fileOverflowGroup);
            }
        }

        private static ProblemSummary CreateProblemSummary(string filePath, IReadOnlyList<MonacoMarker> markers) {
            var displayedItems = new List<ProblemItem>(MaxVisibleProblemsPerFile);
            var totalCount = 0;
            var errorCount = 0;
            var warningCount = 0;

            foreach (var marker in markers) {
                if (marker.Severity is not (4 or 8)) continue;

                totalCount++;
                if (marker.Severity == 8) {
                    errorCount++;
                }
                else {
                    warningCount++;
                }

                if (displayedItems.Count < MaxVisibleProblemsPerFile) {
                    displayedItems.Add(new ProblemItem(marker, filePath));
                }
            }

            displayedItems = displayedItems
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.LineNumber)
                .ThenBy(item => item.ColumnNumber)
                .ToList();

            return new ProblemSummary(displayedItems, totalCount, errorCount, warningCount, totalCount > MaxVisibleProblemsPerFile);
        }

        private string GetRelativeDirectory(string filePath) {
            if (string.IsNullOrEmpty(_projectFolder) || string.IsNullOrEmpty(filePath))
                return string.Empty;

            var normalizedBase = Path.GetFullPath(_projectFolder);
            var normalizedTarget = Path.GetFullPath(Path.GetDirectoryName(filePath) ?? _projectFolder);

            var relativePath = Path.GetRelativePath(normalizedBase, normalizedTarget);
            return relativePath == "." ? string.Empty : relativePath;
        }

        private void ProblemsTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args) {
            if (args.InvokedItem is ProblemFileGroup group) {
                group.IsExpanded = !group.IsExpanded;
            }
            else if (args.InvokedItem is ProblemItem item) {
                ProblemRequested?.Invoke(this, item);
            }
        }

        private string _projectFolder = string.Empty;
        private ProblemFileGroup? _fileOverflowGroup;
        private readonly Dictionary<string, ProblemSummary> _hiddenProblemFiles = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record ProblemSummary(
        IReadOnlyList<ProblemItem> DisplayedItems,
        int TotalCount,
        int ErrorCount,
        int WarningCount,
        bool HasMore);

    public partial class ProblemFileGroup : ObservableObject {
        public ProblemFileGroup(string filePath, string relativeDirectory, ProblemSummary summary) {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            RelativeDirectory = relativeDirectory;
            Items = [];
            IsExpanded = true;
            SetItems(summary);
        }

        private ProblemFileGroup(string text) {
            FilePath = string.Empty;
            FileName = "…";
            RelativeDirectory = text;
            Items = [];
            IsExpanded = false;
            IsOverflow = true;
        }

        public static ProblemFileGroup CreateOverflow(string text) => new(text);

        public string FilePath { get; }
        public string FileName { get; private set; }
        public string RelativeDirectory { get; private set; }
        public bool IsOverflow { get; }
        public int TotalCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        private string IconResourceKey => WebEditorFileUtil.GetIconResourceKeyFromExtension(Path.GetExtension(FilePath));
        public string IconDataResourceKey => $"{IconResourceKey}_Data";
        public Brush? IconBrush =>
            _iconBrush ??= GetResource<Brush>($"{IconResourceKey}_Brush");
        public ObservableCollection<object> Items { get; }
        public string CountText => IsOverflow ? string.Empty : TotalCount.ToString();

        private static T? GetResource<T>(string resourceKey) where T : class =>
            Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            && resource is T typedResource
                ? typedResource
                : null;

        private Brush? _iconBrush;

        public bool HasSameItems(ProblemSummary summary) {
            if (TotalCount != summary.TotalCount
                || ErrorCount != summary.ErrorCount
                || WarningCount != summary.WarningCount
                || Items.OfType<ProblemOverflowItem>().Any() != summary.HasMore) {
                return false;
            }

            var currentItems = Items.OfType<ProblemItem>().ToList();
            if (currentItems.Count != summary.DisplayedItems.Count) return false;

            for (var i = 0; i < currentItems.Count; i++) {
                if (!currentItems[i].EqualsByValue(summary.DisplayedItems[i])) return false;
            }

            return true;
        }

        public void SetItems(ProblemSummary summary) {
            TotalCount = summary.TotalCount;
            ErrorCount = summary.ErrorCount;
            WarningCount = summary.WarningCount;

            Items.Clear();
            foreach (var item in summary.DisplayedItems) {
                Items.Add(item);
            }
            if (summary.HasMore) {
                Items.Add(new ProblemOverflowItem(string.Format(
                    LanguageUtil.GetI18n("WebBackdrop_HiddenProblems"),
                    TotalCount - summary.DisplayedItems.Count)));
            }

            OnPropertyChanged(nameof(CountText));
        }

        public void SetOverflowText(string text) {
            RelativeDirectory = text;
            OnPropertyChanged(nameof(RelativeDirectory));
        }

        public bool IsExpanded {
            get => _isExpanded;
            set {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        private bool _isExpanded;
    }

    public sealed record ProblemOverflowItem(string Text);

    public enum ProblemSeverity {
        Warning,
        Error,
    }

    public sealed class ProblemItem {
        public ProblemItem(MonacoMarker marker, string filePath) {
            FilePath = filePath;
            Severity = marker.Severity == 8 ? ProblemSeverity.Error : ProblemSeverity.Warning;
            Message = marker.Message;
            SourceText = GetSourceText(marker.Source, marker.Code);
            LineNumber = marker.StartLineNumber;
            ColumnNumber = marker.StartColumn;
            PositionText = $"[Ln {LineNumber}, Col {ColumnNumber}]";
        }

        public string FilePath { get; }
        public ProblemSeverity Severity { get; }
        public string Message { get; }
        public string SourceText { get; }
        public int LineNumber { get; }
        public int ColumnNumber { get; }
        public string PositionText { get; }
        public string Glyph => Severity == ProblemSeverity.Error ? "\uEA39" : "\uE7BA";
        public Visibility ErrorVisibility => Severity == ProblemSeverity.Error ? Visibility.Visible : Visibility.Collapsed;
        public Visibility WarningVisibility => Severity == ProblemSeverity.Warning ? Visibility.Visible : Visibility.Collapsed;

        public bool EqualsByValue(ProblemItem other) {
            return FilePath == other.FilePath
                && Severity == other.Severity
                && Message == other.Message
                && SourceText == other.SourceText
                && LineNumber == other.LineNumber
                && ColumnNumber == other.ColumnNumber;
        }

        private static string GetSourceText(string source, string code) {
            if (string.IsNullOrEmpty(source)) return code;
            if (string.IsNullOrEmpty(code)) return source;
            return $"{source}({code})";
        }
    }

    public sealed partial class ProblemTreeItemTemplateSelector : DataTemplateSelector {
        public DataTemplate? FileTemplate { get; set; }
        public DataTemplate? ProblemTemplate { get; set; }
        public DataTemplate? OverflowTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item) {
            return item switch {
                ProblemFileGroup => FileTemplate,
                ProblemOverflowItem => OverflowTemplate,
                _ => ProblemTemplate,
            };
        }
    }
}
