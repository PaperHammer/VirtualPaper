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
    public sealed class DynamicImageAddTaskViewModel : ObservableObject {
        internal ResettableCompletionSource<IIntelliData?>? IntelligentCTS { get; set; }
        public Action? CardUIStateChanged { get; set; }
        public string PreviousStepBtnText { get; private set; } = string.Empty;
        public string NextStepBtnText { get; private set; } = string.Empty;
        public bool BtnVisible { get; private set; }

        private bool _isNextEnable;
        public bool IsNextEnable {
            get => _isNextEnable;
            private set {
                if (_isNextEnable == value)
                    return;
                _isNextEnable = value;
                OnPropertyChanged();
                CardUIStateChanged?.Invoke();
            }
        }

        private string? _sourceFilePath;
        public string? SourceFilePath {
            get => _sourceFilePath;
            private set {
                _sourceFilePath = value;
                OnPropertyChanged();
                UpdateNextEnable();
            }
        }
        public string? SourceFileSize { get; private set; }
        public string? SourceFileExt { get; private set; }
        public string? SourceFileResolution { get; private set; }

        private bool _isHighQuality;
        public bool IsHighQuality {
            get => _isHighQuality;
            set {
                if (_isHighQuality == value)
                    return;
                _isHighQuality = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBalanced));
            }
        }
        public bool IsBalanced {
            get => !IsHighQuality;
            set {
                if (value)
                    IsHighQuality = false;
            }
        }

        public void UpdateCardComponentUI() {
            PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel));
            NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));
            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        public Task OnPreviousStepClickedAsync() {
            IntelligentCTS?.TrySetResult(null);
            return Task.CompletedTask;
        }

        public Task OnNextStepClickedAsync() {
            if (SourceFilePath is null || SourceFileSize is null || SourceFileExt is null)
                return Task.CompletedTask;

            IntelligentCTS?.TrySetResult(new DynamicImageData(
                SourceFilePath,
                SourceFileSize,
                SourceFileExt,
                _width,
                _height,
                IsHighQuality ? DynamicImageQuality.High : DynamicImageQuality.Balanced));
            return Task.CompletedTask;
        }

        public async Task SelectSourceImageAsync() {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FImageAI]]);
            if (storage == null || storage.Length == 0)
                return;

            string path = storage[0].Path;
            if (FileFilter.GetFileTypeFroImageAI(path) != FileType.FImageAI) {
                GlobalMessageUtil.ShowError(LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Error_InvalidFile)));
                return;
            }

            SourceFilePath = path;
            SourceFileSize = FileUtil.GetFileSize(path);
            SourceFileExt = Path.GetExtension(path).ToLowerInvariant();
            (_width, _height) = await FileUtil.GetImageResolutionAsync(path);
            SourceFileResolution = $"{_width} × {_height}";
            OnPropertyChanged(nameof(SourceFileSize));
            OnPropertyChanged(nameof(SourceFileExt));
            OnPropertyChanged(nameof(SourceFileResolution));
            UpdateNextEnable();
        }

        public void Clean() {
            SourceFilePath = null;
            SourceFileSize = null;
            SourceFileExt = null;
            SourceFileResolution = null;
            _width = 0;
            _height = 0;
            IsHighQuality = false;
            OnPropertyChanged(nameof(SourceFileSize));
            OnPropertyChanged(nameof(SourceFileExt));
            OnPropertyChanged(nameof(SourceFileResolution));
            UpdateNextEnable();
        }

        internal void SetSourceForTest(string path, string size, string extension, uint width, uint height) {
            SourceFileSize = size;
            SourceFileExt = extension;
            _width = width;
            _height = height;
            SourceFileResolution = $"{width} × {height}";
            SourceFilePath = path;
        }

        private void UpdateNextEnable() =>
            IsNextEnable = !string.IsNullOrWhiteSpace(SourceFilePath) && _width > 0 && _height > 0;

        private uint _width;
        private uint _height;
    }
}
