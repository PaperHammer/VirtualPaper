namespace VirtualPaper.Common.Runtime.PlayerWeb {
    public static class Fields {
        public static string ApplyFilter { get; } = "applyFilter";
        public static string Play { get; } = "play";
        public static string PlaybackChanged { get; } = "playbackChanged";
        public static string AudioMuteChanged { get; } = "audioMuteChanged";
        public static string PropertyListener { get; } = "propertyListener";
        public static string MouseMove { get; } = "mouseMove";
        public static string MouseOut { get; } = "mouseOut";
        public static string ResourceLoad { get; } = "resourceLoad";
        public static string UpdateDimensions { get; } = "updateDimensions";
        public static string TimePerception { get; } = "TimePerception";

        // RWeb 平台 API：对应 window.wallpaper 对象的内部方法
        public static string WpApiSetPaused { get; } = "window.wallpaper.__setPaused";
        public static string WpApiApplyProperties { get; } = "window.wallpaper.__applyProperties";
        public static string WpApiMouseMove { get; } = "window.wallpaper.__mouseMove";
    }
}
