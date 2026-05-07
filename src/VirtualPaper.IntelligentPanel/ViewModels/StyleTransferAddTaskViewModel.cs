using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.IntelligentPanel.ViewModels {
    public partial class StyleTransferAddTaskViewModel : ObservableObject {
        private bool _isSourceImageLoaded;
        public bool IsSourceImageLoaded {
            get { return _isSourceImageLoaded; }
            set { _isSourceImageLoaded = value; OnPropertyChanged(); }
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

        private bool _isStyleImageLoaded;
        public bool IsStyleImageLoaded {
            get { return _isStyleImageLoaded; }
            set { _isStyleImageLoaded = value; OnPropertyChanged(); }
        }

        private string? _styleFileSize;
        public string? StyleFileSize {
            get { return _styleFileSize; }
            set { _styleFileSize = value; OnPropertyChanged(); }
        }

        private string? _styleFileExt;
        public string? StyleFileExt {
            get { return _styleFileExt; }
            set { _styleFileExt = value; OnPropertyChanged(); }
        }

        private string? _estimatedTimeText;
        public string? EstimatedTimeText {
            get { return _estimatedTimeText; }
            set { _estimatedTimeText = value; OnPropertyChanged(); }
        }

        private StyleOptionItem _selectedStyle = null!;
        public StyleOptionItem SelectedStyle {
            get { return _selectedStyle; }
            set { _selectedStyle = value; OnPropertyChanged(); }
        }

        public StyleTransferAddTaskViewModel() {
            InitCollectoins();
        }

        private void InitCollectoins() {
            StyleOptions = [
                new StyleOptionItem {
                    Name = "动漫",
                    ThumbnailResourceKey = "PresetStyles_Anime",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/anime.jpg",
                    FileSize = "751 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "卡通",
                    ThumbnailResourceKey = "PresetStyles_Cartoon",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/cartoon.jpg",
                    FileSize = "557 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "工笔画",
                    ThumbnailResourceKey = "PresetStyles_Gongbi",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/gongbi.jpg",
                    FileSize = "671 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "铅笔画",
                    ThumbnailResourceKey = "PresetStyles_Pencil",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/pencil.jpg",
                    FileSize = "931 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "水彩",
                    ThumbnailResourceKey = "PresetStyles_Watercolor",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/watercolor.jpg",
                    FileSize = "888 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "水墨画",
                    ThumbnailResourceKey = "PresetStyles_InkWash",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/ink_wash.jpg",
                    FileSize = "602 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "油画",
                    ThumbnailResourceKey = "PresetStyles_OilPainting",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/oil_painting.jpg",
                    FileSize = "853 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "彩铅",
                    ThumbnailResourceKey = "PresetStyles_ColoredPencil",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/colored_pencil.jpg",
                    FileSize = "999 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "白描",
                    ThumbnailResourceKey = "PresetStyles_OutlineDrawing",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/outline_drawing.jpg",
                    FileSize = "952 KB",
                    FileExt = ".jpg",
                },
                new StyleOptionItem {
                    Name = "浮世绘",
                    ThumbnailResourceKey = "PresetStyles_Ukiyoe",
                    ImagePath = "ms-appx:///Assets/intelligent/preset_styles/ukiyo-e.jpg",
                    FileSize = "955 KB",
                    FileExt = ".jpg",
                },
                // 最后一项：自定义
                new StyleOptionItem {
                    Name = "自定义",
                    IsCustom = true,
                },
            ];

            SelectedStyle = StyleOptions[0];
        }

        internal StyleOptionItem[] StyleOptions = null!;
    }
}
