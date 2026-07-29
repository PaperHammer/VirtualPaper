using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.UIComponent.ViewModels {
    public partial class AddFileItemViewModel : ObservableObject {
        private string? _newName;
        public string? NewName {
            get { return _newName; }
            set {
                _newName = value;
                IsNameOk = ComplianceUtil.IsValidPathSegmentName(value, 1, _maxLength, _onlyLength);
                OnPropertyChanged();
            }
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        public AddFileItemViewModel(string defaultName, int maxLength, bool onlyLength) {
            _maxLength = maxLength;
            _onlyLength = onlyLength;
            NewName = defaultName;
        }

        private readonly int _maxLength;
        private readonly bool _onlyLength;
    }
}
