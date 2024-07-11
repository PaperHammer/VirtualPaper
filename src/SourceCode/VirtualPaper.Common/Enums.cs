using System.ComponentModel;

namespace VirtualPaper.Common
{
    #region for App
    public enum AppWpRunRulesEnum
    {
        Silence,
        Pause,
        KeepRun,
    }

    public enum AppTheme
    {
        Auto,
        Light,
        Dark,
    }

    public enum AppSystemBackdrop
    {
        Default,
        Mica,
        Acrylic
    }

    public enum ScrEffect
    {
        [Description("None")]
        None,
        [Description("Bubble")]
        Bubble,
    }
    #endregion

    #region play utils
    public enum PlaybackMode
    {
        [Description("All Wallpapers Silence")]
        Silence,
        [Description("All Wallpapers Paused")]
        Paused,
        [Description("Normal")]
        Play
    }

    public enum StatuMechanismEnum
    {
        Per,
        All
    }

    /// <summary>
    /// Similar to <see href="https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.stretch?view=netcore-3.1">System.Media.Stretch</see>
    /// </summary>
    public enum WallpaperScaler
    {
        none,
        fill,
        uniform,
        uniformFill,
        auto
    }
    #endregion 

    #region input
    public enum InputForwardMode
    {
        off,
        mouse,
        mousekeyboard,
    }
    #endregion

    #region screen settings
    public enum WallpaperArrangement
    {
        /// <summary>
        /// 各显示器独立显示
        /// </summary>
        [Description("Per Display")]
        Per,
        /// <summary>
        /// 同一壁纸跨越多个显示器
        /// </summary>
        [Description("Expand Across All Display(s)")]
        Expand,
        /// <summary>
        /// 复制主显示器壁纸
        /// </summary>
        [Description("Same wp for All Display(s)")]
        Duplicate
    }
    #endregion

    #region wallpaper type
    public enum WallpaperType
    {
        [Description("Unknown")]
        unknown,

        /// <summary>
        /// 应用程序
        /// </summary>
        [Description("Application")]
        app,

        /// <summary>
        /// 网页
        /// </summary>
        [Description("Webpage")]
        web,

        /// <summary>
        /// 带有音频可视化功能的网页
        /// </summary>
        [Description("Webpage Audio Visualiser")]
        webaudio,

        //[Description("Webpage Link")] //"ValueType" tab only, not for "Library"! 
        //url,

        /// <summary>
        /// Bizhawk 模拟器
        /// </summary>
        [Description("Bizhawk Emulator")]
        bizhawk,

        /// <summary>
        /// Unity 游戏
        /// </summary>
        [Description("Unity Game")]
        unity,

        /// <summary>
        /// 带有音频可视化的Unity应用
        /// </summary>
        [Description("Unity Audio Visualiser")]
        unityaudio,

        /// <summary>
        /// Godot 游戏
        /// </summary>
        [Description("Godot Game")]
        godot,

        /// <summary>
        /// 动态GIF图片
        /// </summary>
        [Description("Animated Gif")]
        gif,

        /// <summary>
        /// 静态图片
        /// </summary>
        [Description("Static picture")]
        picture,

        /// <summary>
        /// 视频
        /// </summary>
        [Description("Video")]
        video,

        /// <summary>
        /// 视频流
        /// </summary>
        //[Description("Video Streams")]
        //videostream,
        /*
        [Description("Animated sequence HEIC file")]
        heic
        */
    }

    public enum ObjectFit
    {
        Fill,
        Contain,
        Cover,
        None,
        ScaleDown
    }
    #endregion

    #region costumise
    public class UniverseCostumise
    {
        public Saturation Saturation { get; set; }
        public Hue Hue { get; set; }
        public Brightness Brightness { get; set; }
        public Contrast Contrast { get; set; }
        public Scaling Scaling { get; set; }

        public UniverseCostumise()
        {
            Saturation = new();
            Hue = new();
            Brightness = new();
            Contrast = new();
            Scaling = new();

            _properties = new Dictionary<string, dynamic>
            {
                { nameof(Saturation), Saturation },
                { nameof(Hue), Hue },
                { nameof(Brightness), Brightness },
                { nameof(Contrast), Contrast },
                { nameof(Scaling), Scaling },
            };
        }

        public void ModifyPropertyValue<T>(string propertyName, T value)
        {
            if (_properties.TryGetValue(propertyName, out dynamic property))
            {
                if (typeof(T) == typeof(bool))
                {
                    property.Value = value;
                }
                else if (value >= property.Min && value <= property.Max)
                {
                    property.Value = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"The value must be between {property.Min} and {property.Max} for the {propertyName} property.");
                }
            }
            else
            {
                throw new ArgumentException($"Invalid property name: {propertyName}");
            }
        }

        protected readonly Dictionary<string, dynamic> _properties;
    }

    public class PictureCostumise : UniverseCostumise
    {
        //public Saturation Saturation { get; set; }
        //public Hue Hue { get; set; }
        //public Brightness Brightness { get; set; }
        //public Contrast Contrast { get; set; }

        //public PictureCostumise()
        //{
        //    //Saturation = new();
        //    //Hue = new();
        //    //Brightness = new();
        //    //Contrast = new();

        //    //_properties = new Dictionary<string, dynamic>
        //    //{
        //    //    { nameof(Saturation), Saturation },
        //    //    { nameof(Hue), Hue },
        //    //    { nameof(Brightness), Brightness },
        //    //    { nameof(Contrast), Contrast },
        //    //    { nameof(Scaling), Scaling },
        //    //};
        //}

        //public void ModifyPropertyValue<T>(string propertyName, T value)
        //{
        //    if (_properties.TryGetValue(propertyName, out dynamic property))
        //    {
        //        if (value >= property.Min && value <= property.Max)
        //        {
        //            property.Value = value;
        //        }
        //        else
        //        {
        //            throw new ArgumentOutOfRangeException(nameof(value), $"The value must be between {property.Min} and {property.Max} for the {propertyName} property.");
        //        }
        //    }
        //    else
        //    {
        //        throw new ArgumentException($"Invalid property name: {propertyName}");
        //    }
        //}

        //private readonly Dictionary<string, dynamic> _properties;
    }

    public class VideoAndGifCostumize : UniverseCostumise
    {
        //public Saturation Saturation { get; set; }
        //public Hue Hue { get; set; }
        //public Brightness Brightness { get; set; }
        //public Contrast Contrast { get; set; }
        public Speed Speed { get; set; }
        public Volume Volume { get; set; }

        public VideoAndGifCostumize()
        {
            //Saturation = new();
            //Hue = new();
            //Brightness = new();
            //Contrast = new();
            Speed = new();
            Volume = new();

            _properties[nameof(Speed)] = Speed;
            _properties[nameof(Volume)] = Volume;
            //_properties = new Dictionary<string, dynamic>
            //{
            //    { nameof(Saturation), Saturation },
            //    { nameof(Hue), Hue },
            //    { nameof(Brightness), Brightness },
            //    { nameof(Contrast), Contrast },
            //    { nameof(Scaling), Scaling },
            //    { nameof(Speed), Speed },
            //    { nameof(Volume), Volume },
            //};
        }

        //public void ModifyPropertyValue<T>(string propertyName, T value)
        //{
        //    if (_properties.TryGetValue(propertyName, out dynamic property))
        //    {
        //        if (typeof(T) == typeof(bool))
        //        {
        //            property.Value = value;
        //        }
        //        else if (value >= property.Min && value <= property.Max)
        //        {
        //            property.Value = value;
        //        }
        //        else
        //        {
        //            throw new ArgumentOutOfRangeException(nameof(value), $"The value must be between {property.Min} and {property.Max} for the {propertyName} property.");
        //        }
        //    }
        //    else
        //    {
        //        throw new ArgumentException($"Invalid property name: {propertyName}");
        //    }
        //}

        //private readonly Dictionary<string, dynamic> _properties;
    }

    public class Saturation
    {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Saturation";
        public double Value { get; set; } = 1;
        public double Max { get; init; } = 10;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.1;
    }

    public class Hue
    {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Hue";
        public double Value { get; set; } = 0;
        public double Max { get; init; } = 359;
        public double Min { get; init; } = 0;
        public int Step { get; init; } = 1;
    }

    public class Brightness
    {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Brightness";
        public double Value { get; set; } = 1;
        public double Max { get; init; } = 2;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.1;
    }

    public class Contrast
    {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Contrast";
        public double Value { get; set; } = 1;
        public double Max { get; init; } = 10;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.1;
    }

    public class Scaling
    {
        public string Type { get; set; } = "Dropdown";
        public string Text { get; set; } = "Scale Way";
        public int Value { get; set; } = 0;
        public List<string> Items { get; set; } = ["Fill", "Contain", "Cover", "None", "Scale-Down"];
        public string Help { get; set; } = "";
    }

    public class Speed
    {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Speed";
        public double Value { get; set; } = 1.0;
        public double Max { get; init; } = 5;
        public double Min { get; init; } = 0.25;
        public double Step { get; init; } = 0.05;
    }

    public class Volume
    {
        public string Type { get; init; } = "Slider";
        public string Text { get; init; } = "Volume";
        public double Value { get; set; } = 0.8;
        public double Max { get; init; } = 1;
        public double Min { get; init; } = 0;
        public double Step { get; init; } = 0.01;
    }
    #endregion
}
