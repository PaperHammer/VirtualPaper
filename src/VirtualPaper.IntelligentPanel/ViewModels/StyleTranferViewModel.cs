using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public partial class StyleTranferViewModel : ObservableObject {
        private readonly ObservableCollection<object> Tasks = [];        

        private bool _hasTasks;
        public bool HasTasks {
            get => _hasTasks;
            set {
                if (_hasTasks == value) return;

                _hasTasks = value;
                OnPropertyChanged();
            }
        }

        public StyleTranferViewModel() {
            InitEvent();
        }

        private void InitEvent() {
            Tasks.CollectionChanged += OnTasksCollectionChanged;

        }

        internal void AddTask(string[]? paths) {
            throw new NotImplementedException();
        }

        private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            HasTasks = Tasks.Count > 0;
        }

        private bool _disposed;
        public void Dispose() {
            if (_disposed) return;

            Tasks.CollectionChanged -= OnTasksCollectionChanged;
            Tasks.Clear();
            _disposed = true;
        }
    }
}
