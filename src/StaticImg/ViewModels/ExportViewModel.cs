using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
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


        private string _exportName = null!;
        public string ExportName {
            get { return _exportName; }
            set {
                if (_exportName == value) return;

                _exportName = value;
                OnPropertyChanged();
                IsNameOk = ComplianceUtil.IsValidName(value);
                IsNextEnable = IsNameOk && IsPathOk;
            }
        }

        private bool _isNameOk;
        public bool IsNameOk {
            get { return _isNameOk; }
            set { _isNameOk = value; OnPropertyChanged(); }
        }

        private string _exportPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string ExportPath {
            get { return _exportPath; }
            set {
                if (_exportPath == value) return;

                _exportPath = value;
                OnPropertyChanged();
                IsPathOk = ComplianceUtil.IsValidFolderPath(value);
                IsNextEnable = IsNameOk && IsPathOk;
            }
        }

        private bool _isPathOk;
        public bool IsPathOk {
            get { return _isPathOk; }
            set { _isPathOk = value; OnPropertyChanged(); }
        }

        private ExportImageFormat _selectedImageFormat = ExportImageFormat.Png;
        public ExportImageFormat SelectedFormat {
            get { return _selectedImageFormat; }
            set {
                if (value == _selectedImageFormat) return;
                _selectedImageFormat = value;
                OnPropertyChanged();
                IsJpegQualityVisible = (value == ExportImageFormat.Jpeg) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public ScaleOption[] ScaleOptions { get; } = [
                new ScaleOption("50%",  0.5,  false ),
                new ScaleOption (  "100%",  1.0,  true ),
                new ScaleOption (  "200%",  2.0,  false ),
                new ScaleOption (  "300%",  3.0,  false )
            ];

        public ExportImageFormat[] AvailableFormats { get; } = [
            ExportImageFormat.Png,
            ExportImageFormat.Bmp,
            ExportImageFormat.Jpeg,
            ExportImageFormat.JpegXR,
        ];

        private ImageSource? _previewImageSource;
        public ImageSource? PreviewImageSource {
            get => _previewImageSource;
            set { _previewImageSource = value; OnPropertyChanged(); }
        }

        private float _jpegQuality = 90f;
        public float JpegQuality {
            get => _jpegQuality;
            set { _jpegQuality = value; OnPropertyChanged(); }
        }

        private Visibility _isJpegQualityVisible = Visibility.Collapsed;
        public Visibility IsJpegQualityVisible {
            get => _isJpegQualityVisible;
            set { _isJpegQualityVisible = value; OnPropertyChanged(); }
        }

        public ICommand? BrowsePathCommand { get; private set; }

        public ExportViewModel() {
            InitCommand();
        }

        private void InitCommand() {
            BrowsePathCommand = new RelayCommand(async () => {
                await BrowsePathAsync();
            });
        }

        internal async Task InitContentAsync(string defaultName) {
            ExportName = defaultName;
        }

        private async Task BrowsePathAsync() {
            var folderPath = (await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle))?.Path;
            if (folderPath == null) return;

            ExportPath = folderPath;
        }

        public double[] GetSelectedScales() {
            return ScaleOptions.Where(opt => opt.IsSelected).Select(opt => opt.Value).ToArray();
        }

        public void UpdateCardComponentUI() {
            PreviousStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel));
            NextStepBtnText = LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Confirm));
            BtnVisible = true;
            CardUIStateChanged?.Invoke();
        }

        public async Task OnNextStepClickedAsync() {
            float? quality = SelectedFormat == ExportImageFormat.Jpeg ? JpegQuality : null;
            var scales = GetSelectedScales();
            var count = scales.Length;
            var exportData = new ExportDataStaticImg(ExportName, ExportPath, SelectedFormat, scales, count, quality);
            DraftConfigTCS?.TrySetResult(exportData);
        }

        public async Task OnPreviousStepClickedAsync() {
            DraftConfigTCS?.TrySetResult(null);
        }

        internal INavigateComponent _navigateComponent = null!;
    }
}
