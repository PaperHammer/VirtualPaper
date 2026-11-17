namespace VirtualPaper.WpSettingsPanel.Utils {
    // A detached form value: opening/canceling the dialog never changes the wallpaper.
    internal record WallpaperDetailsEdit(string Title, string Description, string Authors,
        string PersonalTags);
}
