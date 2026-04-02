using VirtualPaper.Models.Mvvm;
using Workloads.Utils.DraftUtils.Interfaces;

namespace Workloads.Utils.DraftUtils.Models {
    public record ExportDataStaticImg(string Name, string Path, ExportImageFormat Format, double[] ScalePercentage, int Count, float? JpegQuality = null) : IExportData;

    public enum ExportImageFormat {
        Png,
        Bmp,
        Jpeg,
        JpegXR,
    }

    public partial class ScaleOption : ObservableObject {
        public string DisplayName { get; }
        public double Value { get; }

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public ScaleOption(string displayName, double value, bool isSelected) {
            DisplayName = displayName;
            Value = value;
            IsSelected = isSelected;
        }
    }
}
