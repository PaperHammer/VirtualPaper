namespace VirtualPaper.UIComponent.Input {
    public enum ArcSliderTrackFillMode {
        /// <summary>仅在拇指左侧（已播放区域）显示自定义颜色，与原生 Slider 行为一致。</summary>
        Progress,

        /// <summary>彩色背景平铺整个轨道，不随拇指位置变化，恒定显示。</summary>
        Full,
    }
}
