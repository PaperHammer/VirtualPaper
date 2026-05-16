namespace VirtualPaper.IntelligentPanel.Models {
    /// <summary>
    /// 图像增强模式
    /// </summary>
    public enum EnhanceMode {
        /// <summary>
        /// 画质修复：保持原始分辨率，去噪/去模糊/去压缩伪影
        /// </summary>
        QualityRestore,

        /// <summary>
        /// 超分放大：提升分辨率
        /// </summary>
        SuperResolution,
    }
}
