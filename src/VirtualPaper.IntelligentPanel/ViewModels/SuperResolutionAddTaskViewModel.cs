using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public partial class SuperResolutionAddTaskViewModel : ObservableObject {
        internal ResettableCompletionSource<IIntelliData?>? IntelligentCTS { get; set; }

        #region card_component
        public Action? CardUIStateChanged { get; set; }
        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public string NextStepBtnText { get; private set; } = string.Empty;
        public bool BtnVisible { get; private set; } = false;

        private bool _isNextEnable;
        public bool IsNextEnable {
            get { return _isNextEnable; }
            set { _isNextEnable = value; CardUIStateChanged?.Invoke(); }
        }

        public void UpdateCardComponentUI() {
            PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel));
            NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));

            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        public async Task OnNextStepClickedAsync() {
            if (string.IsNullOrEmpty(SourceFilePath)) return;

            uint targetWidth, targetHeight;
            EnhanceMode mode;
            int magnification;

            if (IsQualityRestoreMode) {
                mode = EnhanceMode.QualityRestore;
                magnification = 1;
                targetWidth = _sourceFileWidth;
                targetHeight = _sourceFileHeight;
            }
            else {
                mode = EnhanceMode.SuperResolution;
                magnification = SelectedMagnification;
                targetWidth = _sourceFileWidth * (uint)magnification;
                targetHeight = _sourceFileHeight * (uint)magnification;
            }

            var input = new SuperResolutionData(
                SourceFilePath,
                SourceFileSize!,
                SourceFileExt!,
                _sourceFileWidth,
                _sourceFileHeight,
                mode,
                magnification,
                targetWidth,
                targetHeight);

            IntelligentCTS?.TrySetResult(input);
        }

        public async Task OnPreviousStepClickedAsync() {
            IntelligentCTS?.TrySetResult(null);
        }
        #endregion

        #region source image
        private string? _sourceFilePath;
        public string? SourceFilePath {
            get { return _sourceFilePath; }
            set {
                _sourceFilePath = value;
                OnPropertyChanged();
                UpdateOutputResolutionText();
                UpdateNextEnable();
            }
        }

        private string? _sourceFileSize;
        public string? SourceFileSize {
            get { return _sourceFileSize; }
            set { _sourceFileSize = value; OnPropertyChanged(); }
        }

        private string? _sourceFileExt;
        public string? SourceFileExt {
            get { return _sourceFileExt; }
            set { _sourceFileExt = value; OnPropertyChanged(); }
        }

        private string? _sourceFileResolution;
        public string? SourceFileResolution {
            get { return _sourceFileResolution; }
            set { _sourceFileResolution = value; OnPropertyChanged(); }
        }

        private uint _sourceFileWidth;
        private uint _sourceFileHeight;
        #endregion

        #region enhance mode
        private bool _isQualityRestoreMode;
        public bool IsQualityRestoreMode {
            get { return _isQualityRestoreMode; }
            set {
                if (_isQualityRestoreMode == value) return;
                _isQualityRestoreMode = value;
                OnPropertyChanged();
                if (value) {
                    IsSuperResolutionMode = false;
                }
                UpdateOutputResolutionText();
                UpdateNextEnable();
            }
        }

        private bool _isSuperResolutionMode = true;
        public bool IsSuperResolutionMode {
            get { return _isSuperResolutionMode; }
            set {
                if (_isSuperResolutionMode == value) return;
                _isSuperResolutionMode = value;
                OnPropertyChanged();
                if (value) {
                    IsQualityRestoreMode = false;
                }
                UpdateOutputResolutionText();
                UpdateNextEnable();
            }
        }
        #endregion

        #region magnification
        private bool _isMag2x = true;
        public bool IsMag2x {
            get { return _isMag2x; }
            set {
                if (_isMag2x == value) return;
                _isMag2x = value;
                OnPropertyChanged();
                if (value) {
                    _isMag4x = false;
                    OnPropertyChanged(nameof(IsMag4x));
                    SelectedMagnification = 2;
                }
            }
        }

        private bool _isMag4x;
        public bool IsMag4x {
            get { return _isMag4x; }
            set {
                if (_isMag4x == value) return;
                _isMag4x = value;
                OnPropertyChanged();
                if (value) {
                    _isMag2x = false;
                    OnPropertyChanged(nameof(IsMag2x));
                    SelectedMagnification = 4;
                }
            }
        }

        private int _selectedMagnification = 2;
        public int SelectedMagnification {
            get { return _selectedMagnification; }
            private set {
                if (_selectedMagnification == value) return;
                _selectedMagnification = value;
                OnPropertyChanged();
                UpdateOutputResolutionText();
                UpdateNextEnable();
            }
        }
        #endregion

        #region output preview
        private string? _outputResolutionText;
        public string? OutputResolutionText {
            get { return _outputResolutionText; }
            set { _outputResolutionText = value; OnPropertyChanged(); }
        }

        private void UpdateOutputResolutionText() {
            if (_sourceFileWidth == 0 || _sourceFileHeight == 0) {
                OutputResolutionText = null;
                return;
            }

            if (IsQualityRestoreMode) {
                OutputResolutionText = $"{_sourceFileWidth} × {_sourceFileHeight}（原始）";
            }
            else {
                uint w = _sourceFileWidth * (uint)SelectedMagnification;
                uint h = _sourceFileHeight * (uint)SelectedMagnification;
                OutputResolutionText = $"{w} × {h}";
            }
        }
        #endregion

        #region validation
        private void UpdateNextEnable() {
            IsNextEnable = !string.IsNullOrEmpty(SourceFilePath)
                && _sourceFileWidth > 0
                && _sourceFileHeight > 0;
        }
        #endregion

        #region actions
        public async Task SelectSourceImageAsync() {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FImage]]);
            if (storage == null || storage.Length < 1) return;

            string filePath = storage[0].Path;

            SourceFilePath = filePath;
            SourceFileExt = Path.GetExtension(filePath)?.ToLower();
            SourceFileSize = FileUtil.GetFileSize(filePath);

            var (width, height) = await FileUtil.GetImageResolutionAsync(filePath);
            _sourceFileWidth = width;
            _sourceFileHeight = height;
            SourceFileResolution = $"{width} × {height}";

            UpdateOutputResolutionText();
            UpdateNextEnable();
        }
        #endregion

        #region clean
        public void Clean() {
            SourceFilePath = null;
            SourceFileSize = null;
            SourceFileExt = null;
            SourceFileResolution = null;
            _sourceFileWidth = 0;
            _sourceFileHeight = 0;

            IsQualityRestoreMode = false;
            IsSuperResolutionMode = true;
            IsMag2x = true;
            IsMag4x = false;

            OutputResolutionText = null;
            IsNextEnable = false;
        }
        #endregion
    }
}