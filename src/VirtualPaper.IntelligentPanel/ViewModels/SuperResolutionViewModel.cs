using System;
using System.Collections.ObjectModel;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public partial class SuperResolutionViewModel : ObservableObject {
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

        public SuperResolutionViewModel() {
            InitEvent();
        }

        private void InitEvent() {
            Tasks.CollectionChanged += (s, e) => {
                HasTasks = Tasks.Count > 0;
            };
        }

        internal void AddTask() {
            throw new NotImplementedException();
        }
    }
}
