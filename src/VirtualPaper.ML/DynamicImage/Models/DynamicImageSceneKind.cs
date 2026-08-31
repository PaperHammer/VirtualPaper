namespace VirtualPaper.ML.DynamicImage.Models {
    /// <summary>
    /// Describes which part of the image should drive the animation. The
    /// analysis pipeline is shared by every kind; this value selects motion
    /// policy rather than a separate model pipeline.
    /// </summary>
    public enum DynamicImageSceneKind {
        PureScene,
        SceneDominant,
        Mixed,
        SubjectDominant
    }
}
