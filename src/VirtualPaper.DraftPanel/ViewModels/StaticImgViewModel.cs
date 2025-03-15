using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VirtualPaper.DraftPanel.Model.Runtime;
using VirtualPaper.DraftPanel.Utils;
using VirtualPaper.Models.Mvvm;
using Windows.Foundation;

namespace VirtualPaper.DraftPanel.ViewModels {
    partial class StaticImgViewModel : ObservableObject {
        internal event EventHandler<double> OnCanvasZoomChanged;

        private string _canvasSizeText;
        public string CanvasSizeText {
            get { return _canvasSizeText; }
            private set { _canvasSizeText = value; OnPropertyChanged(); }
        }

        private string _pointerPosText;
        public string PointerPosText {
            get { return _pointerPosText; }
            private set { _pointerPosText = value; OnPropertyChanged(); }
        }

        private double _canvasZoom; // 0.2 -- 7.0
        public double CanvasZoom {
            get { return _canvasZoom; }
            set {
                //var newValue = Math.Round(value, 1);
                Debug.WriteLine(value);
                if (!StaticImgMetadata.IsZoomValid(value) || _canvasZoom == value) return;

                _canvasZoom = value;
                OnPropertyChanged();
                OnCanvasZoomChanged?.Invoke(this, value);
            }
        }

        private SizeF _canvasSize;
        public SizeF CanvasSize {
            get { return _canvasSize; }
            set {
                if (_canvasSize == value) return;

                _canvasSize = value;
                CanvasSizeText = $"{value.Width}, {value.Height} {"像素"} ({value.Dpi} Dpi)";
                OnPropertyChanged();
            }
        }

        private Point? _ponterPos;
        public Point? PointerPos {
            get { return _ponterPos; }
            set {
                _ponterPos = value;
                PointF? formatPos = value == null ? null : CanvasUtil.FormatPoint(value, 0);
                PointerPosText = value == null ? string.Empty : $"{formatPos.Value.X}, {formatPos.Value.Y} {"像素"}";
            }
        }

        public StaticImgViewModel(string folderPath) {
            this._folderPath = folderPath;
        }

        public async Task SaveAsync() {
            await StaticImgMetadata.SaveAsync(_folderPath, _vpCanvas);
        }

        public async Task LoadAsync() {
            _vpCanvas = await StaticImgMetadata.LoadAsync(_folderPath);
            InitValue();
        }

        private void InitValue() {
            CanvasSize = _vpCanvas.Size;
        }

        internal VpCanvas _vpCanvas;
        internal readonly string[] _comboZoomFactors = ["700%", "600%", "500%", "400%", "300%", "200%", "100%", "75%", "50%", "25%"];
        private readonly string _folderPath;
    }
}
