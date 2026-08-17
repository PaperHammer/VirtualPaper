using VirtualPaper.Models.Mvvm;
using Workloads.Utils.DraftUtils.Interfaces;

namespace Workloads.Utils.DraftUtils.Models {
    public record ExportDataStaticImg(string Name, string Path, ExportImageFormat Format) : IExportData;

    public enum ExportImageFormat {
        Png,
        Bmp,
        Jpeg,
        JpegXR,
        /// <summary>Web 壁纸包（FWebZip，.zip）——WebBackdrop 等 Web 项目导出用</summary>
        Zip,
        /// <summary>Web 项目全量归档（.zip，含 .vpw 工程文件）——用于备份/迁移</summary>
        FullZip,
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
