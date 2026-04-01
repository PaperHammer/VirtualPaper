using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Creation.StaticImg.ViewModels {
    public partial class ExportViewModel : ObservableObject {
        public Action? CardUIStateChanged { get; set; }
        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public string NextStepBtnText { get; private set; } = string.Empty;
        public bool BtnVisible { get; private set; } = false;
        public TaskCompletionSource<ExportDataStaticImg?>? DraftConfigTCS { get; set; }

        private bool _isNextEnable;
        public bool IsNextEnable {
            get { return _isNextEnable; }
            set { _isNextEnable = value; CardUIStateChanged?.Invoke(); }
        }


        private string? _exportName;
        public string? ExportName {
            get { return _exportName; }
            set {
                if (_exportName == value) return;

                _exportName = value;
                OnPropertyChanged();
                IsNameOk = ComplianceUtil.IsValidName(value);
                IsNextEnable = IsNameOk && IsDirOk;
            }
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        private string? _exportDir;
        public string? ExpotrDir {
            get { return _exportDir; }
            set {
                if (_exportDir == value) return;

                _exportDir = value;
                OnPropertyChanged();
                IsDirOk = ComplianceUtil.IsValidFolderPath(value);
                IsNextEnable = IsNameOk && IsDirOk;
            }
        }

        private bool _isDirOk;
        public bool IsDirOk {
            get { return _isDirOk; }
            set { _isDirOk = value; OnPropertyChanged(); }
        }

        public ExportViewModel() {

        }

        internal async Task InitContentAsync() {
            throw new NotImplementedException();
        }

        public void UpdateCardComponentUI() {
            PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel));
            NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));
            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        public async Task OnNextStepClickedAsync() {
            var exportData = new ExportDataStaticImg(ExportName, ExpotrDir);
            DraftConfigTCS?.TrySetResult(exportData);
        }

        public async Task OnPreviousStepClickedAsync() {
            DraftConfigTCS?.TrySetResult(null);
        }

        internal INavigateComponent _navigateComponent = null!;
    }
}
