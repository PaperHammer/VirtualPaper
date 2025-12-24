using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.PlayerWeb.Core.ViewModels {
    partial class GeneralInfoEditViewModel : ObservableObject {
        #region wallpaper data editable
        private string? _title;
        public string? Title {
            get => _title;
            set {
                if (_title == value) return;
                _title = value;
                if (!string.IsNullOrEmpty(_title)) {
                    OnPropertyChanged();
                }
            }
        }

        private string? _desc;
        public string? Desc {
            get => _desc;
            set {
                if (_desc == value) return;
                _desc = value;
                if (!string.IsNullOrEmpty(_desc)) {
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> TagList { get; set; } = [];
        #endregion

        public ICommand? SaveCommand { get; private set; }

        public GeneralInfoEditViewModel() {
            InitCommand();
        }

        private void InitCommand() {
            SaveCommand = new RelayCommand(async () => {
                if (_data == null) return;
                _data.Title = Title ?? string.Empty;
                _data.Desc = Desc ?? string.Empty;
                _data.Tags = string.Join(';', TagList);
                await _data.SaveAsync();
            });
        }

        internal void InitData(IWpBasicData wpBasicData) {
            _data = wpBasicData;

            if (_data == null) return;
            Title = wpBasicData.Title;
            Desc = wpBasicData.Desc;
            TagList = [.. wpBasicData.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries)];
        }

        private IWpBasicData _data = null!;
    }
}
