namespace VirtualPaper.Models.Histories {
    public static class Consts {

    }

    /// <summary>
    /// Mode of History
    /// </summary>
    public enum HistoryMode {
        /// <summary>
        /// Normal
        /// </summary>
        None,

        /// <summary>
        /// Mult-History
        /// </summary>
        Composite,

        /// <summary>
        /// Setup Layers,
        /// </summary>
        Setup,

        /// <summary>
        /// Add or Remove Layer(s),
        /// </summary>
        Arrange,

        /// <summary>
        /// Property of Layer
        /// </summary>
        Property,
    }

    /// <summary>
    /// Mode of <see cref="PropertyHistory"/>
    /// </summary>
    public enum HistoryPropertyMode {
        /// <summary> Normal </summary>
        None,

        /// <summary> <see cref="double"/> </summary>
        Opacity,
        /// <summary> <see cref="BlendEffectMode"/> </summary>
        BlendMode,
        /// <summary> <see cref="Windows.UI.Xaml.Visibility"/> </summary>
        Visibility,

        /// <summary> <see cref="IDictionary{int, IBuffer}"/> </summary>
        Bitmap,
        /// <summary> <see cref="IBuffer"/> to <see cref="Windows.UI.Color"/> </summary>
        BitmapClear,
        /// <summary> <see cref="IBuffer"/> </summary>
        BitmapReset,
    }
}
