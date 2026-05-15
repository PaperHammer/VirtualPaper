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
    public partial class StyleTransferAddTaskViewModel : ObservableObject {
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
            if (string.IsNullOrEmpty(SourceFilePath) || string.IsNullOrEmpty(SelectedStyle.ImagePath)) return;

            string imgFilePath = SelectedStyle.IsCustom ? SelectedStyle.ImagePath! : (await FileUtil.GetAppxFileAsync(SelectedStyle.ImagePath!))?.Path ?? throw new FileNotFoundException("Appx file not found.");
            var input = new StyleTransferData(
                SourceFilePath, SourceFileSize!, SourceFileExt!, _sourceFileWidth, _sourceFileHeight,
                imgFilePath, SelectedStyle.Name, SelectedStyle.FileSize ?? string.Empty, SelectedStyle.FileExt ?? string.Empty);
            IntelligentCTS?.TrySetResult(input);
        }

        public async Task OnPreviousStepClickedAsync() {
            IntelligentCTS?.TrySetResult(null);
        }
        #endregion

        private string? _sourceFilePath;
        public string? SourceFilePath {
            get { return _sourceFilePath; }
            set { _sourceFilePath = value; OnPropertyChanged(); IsNextEnable = value != null && SelectedStyle != null && SelectedStyle.ImagePath != null; }
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

        private StyleOptionItem _selectedStyle = null!;
        public StyleOptionItem SelectedStyle {
            get { return _selectedStyle; }
            set {
                if (value == null || _selectedStyle == value) return;
                _selectedStyle = value; OnPropertyChanged();
                IsNextEnable = SourceFilePath != null && SelectedStyle != null && SelectedStyle.ImagePath != null;
            }
        }

        public StyleTransferAddTaskViewModel() {
            InitCollectoins();
        }

        private void InitCollectoins() {
            StyleOptions = [
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Anime)),
                    ThumbnailResourceKey = "PresetStyles_Anime",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/anime.jpg",
                    FileSize = "751 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Cartoon)),
                    ThumbnailResourceKey = "PresetStyles_Cartoon",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/cartoon.jpg",
                    FileSize = "557 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Gongbi)),
                    ThumbnailResourceKey = "PresetStyles_Gongbi",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/gongbi.jpg",
                    FileSize = "671 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Pencil)),
                    ThumbnailResourceKey = "PresetStyles_Pencil",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/pencil.jpg",
                    FileSize = "931 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Watercolor)),
                    ThumbnailResourceKey = "PresetStyles_Watercolor",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/watercolor.jpg",
                    FileSize = "888 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_InkWash)),
                    ThumbnailResourceKey = "PresetStyles_InkWash",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/ink_wash.jpg",
                    FileSize = "602 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_OilPainting)),
                    ThumbnailResourceKey = "PresetStyles_OilPainting",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/oil_painting.jpg",
                    FileSize = "853 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_ColoredPencil)),
                    ThumbnailResourceKey = "PresetStyles_ColoredPencil",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/colored_pencil.jpg",
                    FileSize = "999 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_OutlineDrawing)),
                    ThumbnailResourceKey = "PresetStyles_OutlineDrawing",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/outline_drawing.jpg",
                    FileSize = "952 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Ukiyoe)),
                    ThumbnailResourceKey = "PresetStyles_Ukiyoe",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/ukiyo-e.jpg",
                    FileSize = "955 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = LanguageUtil.GetI18n(nameof(Constants.I18n.Intelligent_Style_Type_Custom)),
                    IsCustom = true,
                },
            ];

            SelectedStyle = StyleOptions[^1];
        }

        internal async Task SelectStyleImageAsync() {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FimageAI]]);
            if (storage == null || storage.Length < 1) return;

            string filePath = storage[0].Path;
            StyleOptions[^1].ImagePath = filePath;
            StyleOptions[^1].FileSize = FileUtil.GetFileSize(filePath);
            StyleOptions[^1].FileExt = Path.GetExtension(filePath)?.ToLower();
            IsNextEnable = SelectedStyle != null && SelectedStyle.ImagePath != null;
        }

        internal async Task SelectSourceImageAsync() {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FimageAI]]);
            if (storage == null || storage.Length < 1) return;

            string filePath = storage[0].Path;

            if (FileFilter.GetFileTypeFroImageAI(filePath) != FileType.FimageAI) {
                GlobalMessageUtil.ShowError(LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Error_InvalidFile)));
                return;
            }

            SourceFilePath = filePath;
            SourceFileSize = FileUtil.GetFileSize(filePath);
            SourceFileExt = Path.GetExtension(filePath)?.ToLower();
            (_sourceFileWidth, _sourceFileHeight) = await FileUtil.GetImageResolutionAsync(filePath);
            SourceFileResolution = $"{_sourceFileWidth} * {_sourceFileHeight}";
        }

        internal void Clean() {
            IntelligentCTS?.Reset();
            SourceFilePath = null;
            SourceFileSize = null;
            SourceFileExt = null;
            SourceFileResolution = null;      
            IsNextEnable = false;
        }

        internal StyleOptionItem[] StyleOptions = null!;
        private uint _sourceFileWidth, _sourceFileHeight;
    }
}
