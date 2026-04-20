namespace VirtualPaper.Launcher {
    public static class Consts {
        public static class ApplicationType {
            public static bool IsTestBuild => false;
            public static bool IsMSIX => new DesktopBridge.Helpers().IsRunningAsUwp();
        }
        public static class I18n {
            public static string? Text_Error { get; }
            public static string? AppUpdater_Update_ExceptionAppUpdateFail { get; }
            public static string? AppUpdater_Update_TitleCancelQuestion { get; }
            public static string? AppUpdater_Update_DescriptionCancelQuestion_ForDownloading { get; }
            public static string? AppUpdater_Update_DescriptionCancelQuestion_ForCompleted { get; }
            public static string? Text_Confirm { get; }
            public static string? Text_Cancel { get; }
            public static string? AppUpdater_ActionButtonText_Ready { get; }
            public static string? AppUpdater_StatusText_Ready { get; }
            public static string? AppUpdater_SpeedText_Ready { get; }
            public static string? AppUpdater_ActionButtonText_Downloading { get; }
            public static string? AppUpdater_StatusText_Downloading { get; }
            public static string? AppUpdater_ActionButtonText_Paused { get; }
            public static string? AppUpdater_StatusText_Paused { get; }
            public static string? AppUpdater_StatusText_Verifying { get; }
            public static string? AppUpdater_ActionButtonText_Completed { get; }
            public static string? AppUpdater_StatusText_Completed { get; }
            public static string? Text_Retry { get; }
            public static string? AppUpdater_StatusText_DownloadFailed { get; }
            public static string? AppUpdater_StatusText_VerifyFailed { get; }
            public static string? AppUpdater_ActionButtonText_Installing { get; }
            public static string? AppUpdater_StatusText_Installing { get; }
            public static string? AppUpdater_StatusText_Installed { get; }
            public static string? Text_Close { get; }
        }
    }
}
