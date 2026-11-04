using System;
using System.Windows.Input;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Mvvm;

namespace Workloads.Creation.WebBackdrop.ViewModels {
    public partial class WebProjectInfoViewModel : ObservableObject {
        public event EventHandler<WpWebProjectData>? SaveRequested;

        public string Title {
            get => _data.Title;
            set {
                if (_data.Title == value) return;
                _data.Title = value;
                OnPropertyChanged();
            }
        }

        public string Desc {
            get => _data.Desc;
            set {
                if (_data.Desc == value) return;
                _data.Desc = value;
                OnPropertyChanged();
            }
        }

        public string Authors {
            get => _data.Authors;
            set {
                if (_data.Authors == value) return;
                _data.Authors = value;
                OnPropertyChanged();
            }
        }

        public string Tags {
            get => _data.Tags;
            set {
                if (_data.Tags == value) return;
                _data.Tags = value;
                OnPropertyChanged();
            }
        }

        public string File {
            get => _data.File;
            set {
                if (_data.File == value) return;
                _data.File = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }

        public WebProjectInfoViewModel() {
            SaveCommand = new RelayCommand(() => SaveRequested?.Invoke(this, _data));
        }

        public void Load(WpWebProjectData data) {
            _data = data;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Desc));
            OnPropertyChanged(nameof(Authors));
            OnPropertyChanged(nameof(Tags));
            OnPropertyChanged(nameof(File));
        }

        private WpWebProjectData _data = null!;
    }
}
