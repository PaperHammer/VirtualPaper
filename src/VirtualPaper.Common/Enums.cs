using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VirtualPaper.Common {
    #region for App
    public enum AppParallaxRulesEnum {
        Pause,
        KeepRun,
    }

    public enum AppWpRunRulesEnum {
        Silence,
        Pause,
        KeepRun,
    }

    public enum AppTheme {
        Auto,
        Light,
        Dark,
    }

    public enum AppSystemBackdrop {
        Default,
        Mica,
        Acrylic
    }

    public enum ScrEffect {
        [Description("None")]
        None,
        [Description("Bubble")]
        Bubble,
    }
    #endregion

    #region play utils
    public enum PlaybackMode {
        [Description("All Wallpapers Silence")]
        Silence,
        [Description("All Wallpapers Paused")]
        Paused,
        [Description("Normal")]
        Play
    }

    public enum StatuMechanismEnum {
        Per,
        All
    }

    /// <summary>
    /// Similar to <see href="https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.stretch?view=netcore-3.1">System.Media.Stretch</see>
    /// </summary>
    public enum WallpaperScaler {
        none,
        fill,
        uniform,
        uniformFill,
        auto
    }
    #endregion 

    #region input
    public enum InputForwardMode {
        off,
        mouse,
        mousekeyboard,
    }
    #endregion

    #region screen settings
    public enum WallpaperArrangement {
        /// <summary>
        /// 各显示器独立显示
        /// </summary>
        [Description("Per Display")]
        Per,
        /// <summary>
        /// 复制主显示器壁纸
        /// </summary>
        [Description("Same wp for All Display(s)")]
        Duplicate,
        /// <summary>
        /// 同一壁纸跨越多个显示器
        /// </summary>
        [Description("Expand Across All Display(s)")]
        Expand,        
    }
    #endregion

    #region type    
    public enum FileType {
        FUnknown,
        FImage,
        FGif,
        FVideo,
        FDesign,
    }
    
    public enum RuntimeType {
        RUnknown,
        RImage,
        RImage3D,
        RVideo,
    }

    public enum WallpaperCreateType {
        None,
        Img,
        DepthImg,
    }

    public enum ObjectFit {
        Fill,
        Contain,
        Cover,
        None,
        ScaleDown
    }

    public enum ProjectType {
        PImage,
    }

    public enum InfoBarType {
        Informational,
        Warning,
        Error,
        Success,
    }

    public enum LogType {
        Info,
        Warn,
        Error,
        Trace,
    }

    public enum ObjectLifetime {
        Singleton,
        Scoped,
        Transient,
    }

    public enum DialogResult {
        None,
        Primary,
        Seconday
    }
    #endregion

    #region draft-panel
    public enum DraftPanelStartupType {
        OpenVpd,
        OpenFile,
        OpenFolder,
        NewVpd,
        TypeConfig,
    }
    
    public enum DraftPanelState {
        Startup,
        ProjectConfig,
        DraftConfig,
        WorkSpace,
    }
    #endregion

    #region common
    public enum VisualStates {
        Normal,
        Selected,
        UnSelected,
        PointerOver,
        DragOver,
    }
    #endregion

    #region costumise
    public class UniverseCostumise {
        [JsonPropertyOrder(1)]
        public Saturation Saturation { get; }

        [JsonPropertyOrder(2)]
        public Hue Hue { get; }

        [JsonPropertyOrder(3)]
        public Brightness Brightness { get; }

        [JsonPropertyOrder(4)]
        public Contrast Contrast { get; }


        public UniverseCostumise() {
            Saturation = new();
            Hue = new();
            Brightness = new();
            Contrast = new();

            _properties = new Dictionary<string, dynamic>
            {
                { nameof(Saturation), Saturation },
                { nameof(Hue), Hue },
                { nameof(Brightness), Brightness },
                { nameof(Contrast), Contrast },
            };
        }

        public void ModifyPropertyValue<T>(string propertyName, T value) {
            if (_properties.TryGetValue(propertyName, out dynamic property)) {
                if (typeof(T) == typeof(bool)) {
                    property.Value = value;
                }
                else if (value >= property.Min && value <= property.Max) {
                    property.Value = value;
                }
                else {
                    throw new ArgumentOutOfRangeException(nameof(value), $"The value must be between {property.Min} and {property.Max} for the {propertyName} property.");
                }
            }
            else {
                throw new ArgumentException($"Invalid property name: {propertyName}");
            }
        }

        protected readonly Dictionary<string, dynamic> _properties;
    }

    [JsonSerializable(typeof(PictureAndGifCostumise))]
    public partial class PictureAndGifCostumiseContext : JsonSerializerContext { }
    public class PictureAndGifCostumise : UniverseCostumise {

        [JsonPropertyOrder(5)]
        public Scaling Scaling { get; }

        [JsonPropertyOrder(6)]
        public Parallax Parallax { get; }

        public PictureAndGifCostumise() {
            Scaling = new();
            Parallax = new();

            _properties[nameof(Parallax)] = Parallax;
            _properties[nameof(Scaling)] = Scaling;
        }
    }

    [JsonSerializable(typeof(VideoCostumize))]
    public partial class VideoCostumizeContext : JsonSerializerContext { }
    public class VideoCostumize : UniverseCostumise {
        [JsonPropertyOrder(8)]
        public Speed Speed { get; }

        [JsonPropertyOrder(9)]
        public Volume Volume { get; }

        [JsonPropertyOrder(10)]
        public Scaling Scaling { get; }

        [JsonPropertyOrder(11)]
        public Parallax Parallax { get; }

        public VideoCostumize() {
            Speed = new();
            Volume = new();
            Parallax = new();
            Scaling = new();

            _properties[nameof(Speed)] = Speed;
            _properties[nameof(Volume)] = Volume;
            _properties[nameof(Parallax)] = Parallax;
            _properties[nameof(Scaling)] = Scaling;
        }
    }

    [JsonSerializable(typeof(Picture3DCostumize))]
    public partial class Picture3DCostumizeContext : JsonSerializerContext { }
    public class Picture3DCostumize : UniverseCostumise {

    }

    public class Saturation {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Saturation";

        private double val = 1;
        public double Value {
            get => val;
            set => val = (value < 0 || value > 10) ? 1 : value;
        }

        public double Max { get; init; } = 10;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.1;
    }

    public class Hue {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Hue";

        private double val = 0;
        public double Value {
            get => val;
            set => val = Math.Min(359, Math.Max(0, value));
        }

        public double Max { get; init; } = 359;
        public double Min { get; init; } = 0;
        public int Step { get; init; } = 1;
    }

    public class Brightness {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Brightness";

        private double val = 1;
        public double Value {
            get => val;
            set => val = (value < 0 || value > 2) ? 1 : value;
        }

        public double Max { get; init; } = 2;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.1;
    }

    public class Contrast {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Contrast";

        private double val = 1;
        public double Value {
            get => val;
            set => val = (value < 0 || value > 10) ? 1 : value;
        }

        public double Max { get; init; } = 10;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.1;
    }

    public class Scaling {
        public string Type { get; init; } = "Dropdown";
        public string Text { get; init; } = "Scale Way";

        private int val = 0;
        public int Value {
            get => val;
            set => val = Math.Min(4, Math.Max(0, value));
        }

        public List<string> Items { get; init; } = ["Fill", "Contain", "Cover", "None", "Scale-Down"];
        public string Help { get; init; } = "Effect_Help_Scaling";
    }

    public class Parallax {
        public string Type { get; init; } = "CheckBox";
        public string Text { get; init; } = "Parallax";
        public bool Value { get; set; } = false;
        public string Help { get; init; } = "Effect_Help_Parallax";
    }

    public class Speed {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Speed";

        private double val = 1;
        public double Value {
            get => val;
            set => val = (value < 0.25 || value > 5) ? 1 : value;
        }

        public double Max { get; init; } = 5;
        public double Min { get; init; } = 0.25;
        public double Step { get; init; } = 0.05;
    }

    public class Volume {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Volume";

        private double val = 0.8;
        public double Value {
            get => val;
            set => val = (value < 0 || value > 1) ? 0.8 : value;
        }

        public double Max { get; init; } = 1;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.01;
    }
    #endregion
}
