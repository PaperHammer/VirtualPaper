namespace VirtualPaper.Common {
    public static class Constants {
        public static class Runtime {
            public static nint MainWindowHwnd { get; set; }
        }
        public static class CommonPaths {
            /// <summary>
            /// 数据存储根目录
            /// </summary>
            public static string AppDataDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper");
            public static string CommonDataDir { get; } = Path.Combine(AppDataDir, "data");

            /// <summary>
            /// 日志
            /// </summary>
            public static string LogDir { get; } = Path.Combine(AppDataDir, "logs");
            public static string LogDirCore { get; } = Path.Combine(LogDir, "Core");
            public static string LogDirUI { get; } = Path.Combine(LogDir, "UI");

            /// <summary>
            /// 临时缓存（预览、临时更换）
            /// </summary>
            public static string TempDir { get; } = Path.Combine(AppDataDir, "temp");

            public static string ExeIconDir { get; } = Path.Combine(CommonDataDir, "exeIcon");

            /// <summary>
            /// 壁纸存储目录
            /// </summary>
            public static string LibraryDir { get; } = Path.Combine(AppDataDir, "Library");

            public static string AppRulesPath { get; } = Path.Combine(AppDataDir, "AppRules.json");
            public static string WallpaperLayoutPath { get; } = Path.Combine(AppDataDir, "WallpaperLayout.json");
            public static string UserSettingsPath { get; } = Path.Combine(AppDataDir, "UserSettings.json");

            public static string TempWebView2Dir { get; } = Path.Combine(AppDataDir, "WebView2");
            public static string TempScrWebView2Dir { get; } = Path.Combine(AppDataDir, "ScrWebView2");
        }

        public static class FolderName {
            /// <summary>
            /// 壁纸文件存储目录（本地导入 + 云端下载） + 壁纸自定义配置文件存储目录
            /// </summary>
            public static string WpStoreFolderName { get; } = "wallpapers";
        }

        public static class WorkingDir {
            public static string ML { get; } = Path.Combine("Plugins", "ML", "Models");
            public static string PlayerWeb { get; } = Path.Combine("Plugins", "PlayerWeb", "win-x64");
            public static string ScrSaver { get; } = Path.Combine("Plugins", "ScrSaver");
            public static string UI { get; } = Path.Combine("Plugins", "UI", "win-x64");
        }

        public static class ModuleName {
            public static string UIComponent { get; } = "VirtualPaper.UIComponent";
            public static string UI { get; } = "VirtualPaper.UI.exe";
            public static string ScrSaver { get; } = "VirtualPaper.ScreenSaver.exe";
            public static string PlayerWeb { get; } = "VirtualPaper.PlayerWeb.exe";
        }

        public static class PlayingFile {
            public static string PlayerWeb3D { get; } = Path.Combine("Web", "3d_depth_map.html");
            public static string PlayerWeb { get; } = Path.Combine("Web", "default.html");
        }

        public static class CoreField {
            public static string AppName { get; } = "VirtualPaper";
            public static string FileVersion { get; } = "0.1.0";
            public static string GrpcPipeServerName { get; } = "Grpc_" + PipeServerName;
            public static string PipeServerName { get; } = UniqueAppUid + Environment.UserName;
            public static string UniqueAppUid { get; } = "Virtual:WALLPAPERSYSTEM";
        }

        public static class ApplicationType {
            public static bool IsTestBuild { get; } = false;
            public static bool IsMSIX { get; } = new DesktopBridge.Helpers().IsRunningAsUwp();
        }

        public static class I18n {
            public static string AppSettings_SelBarItem1_General { get; } = "AppSettings_SelBarItem1_General";
            public static string AppSettings_SelBarItem2_Performance { get; } = "AppSettings_SelBarItem2_Performance";
            public static string AppSettings_SelBarItem3_System { get; } = "AppSettings_SelBarItem3_System";
            public static string AppSettings_SelBarItem4_Others { get; } = "AppSettings_SelBarItem4_Others";
            public static string Dialog_Content_ApplyError { get; } = "Dialog_Content_ApplyError";
            public static string Dialog_Content_GetMonitorsAsync { get; } = "Dialog_Content_GetMonitorsAsync";
            public static string Dialog_Content_Import_Failed_Lib { get; } = "Dialog_Content_Import_Failed_Lib";
            public static string Dialog_Content_Import_NeedUpdate { get; } = "Dialog_Content_Import_NeedUpdate";
            public static string Dialog_Content_LibraryDelete { get; } = "Dialog_Content_LibraryDelete";
            public static string Dialog_Content_OnlyPictureAndGif { get; } = "Dialog_Content_OnlyPictureAndGif";
            public static string Dialog_Content_WallpaperDirectoryChangePathInvalid { get; } = "Dialog_Content_WallpaperDirectoryChangePathInvalid";
            public static string Dialog_Content_WpIsUsing { get; } = "Dialog_Content_WpIsUsing";
            public static string Dialog_Title_CreateType { get; } = "Dialog_Title_CreateType";
            public static string Dialog_Title_Prompt { get; } = "Dialog_Title_Prompt";
            public static string Effect_Help_Parallax { get; } = "Effect_Help_Parallax";
            public static string Effect_Help_Scaling { get; } = "Effect_Help_Scaling";
            public static string InfobarMsg_Cancel { get; } = "InfobarMsg_Cancel";
            public static string InfobarMsg_Err { get; } = "InfobarMsg_Err";
            public static string InfobarMsg_ImportErr { get; } = "InfobarMsg_ImportErr";
            public static string InfobarMsg_Success { get; } = "InfobarMsg_Success";
            public static string ScreenSaver__effectBubble { get; } = "ScreenSaver__effectBubble";
            public static string ScreenSaver__effectNone { get; } = "ScreenSaver__effectNone";
            public static string ScreenSaver_Add { get; } = "ScreenSaver_Add";
            public static string ScreenSaver_DynamicEffects { get; } = "ScreenSaver_DynamicEffects";
            public static string ScreenSaver_DynamicEffectsExplain { get; } = "ScreenSaver_DynamicEffectsExplain";
            public static string ScreenSaver_RunningLock { get; } = "ScreenSaver_RunningLock";
            public static string ScreenSaver_RunningLockExplain { get; } = "ScreenSaver_RunningLockExplain";
            public static string ScreenSaver_SeekFromList { get; } = "ScreenSaver_SeekFromList";
            public static string ScreenSaver_Server { get; } = "ScreenSaver_Server";
            public static string ScreenSaver_ServerExplain { get; } = "ScreenSaver_ServerExplain";
            public static string ScreenSaver_WaitingTime { get; } = "ScreenSaver_WaitingTime";
            public static string ScreenSaver_WhiteListExplain { get; } = "ScreenSaver_WhiteListExplain";
            public static string ScreenSaver_WhiteListTitle { get; } = "ScreenSaver_WhiteListTitle";
            public static string Settings_General_AppearanceAndAction__sysbdAcrylic { get; } = "Settings_General_AppearanceAndAction__sysbdAcrylic";
            public static string Settings_General_AppearanceAndAction__sysbdDefault { get; } = "Settings_General_AppearanceAndAction__sysbdDefault";
            public static string Settings_General_AppearanceAndAction__sysbdMica { get; } = "Settings_General_AppearanceAndAction__sysbdMica";
            public static string Settings_General_AppearanceAndAction__themeDark { get; } = "Settings_General_AppearanceAndAction__themeDark";
            public static string Settings_General_AppearanceAndAction__themeFollowSystem { get; } = "Settings_General_AppearanceAndAction__themeFollowSystem";
            public static string Settings_General_AppearanceAndAction__themeLight { get; } = "Settings_General_AppearanceAndAction__themeLight";
            public static string Settings_General_AppearanceAndAction_AppFileStorage { get; } = "Settings_General_AppearanceAndAction_AppFileStorage";
            public static string Settings_General_AppearanceAndAction_AppFileStorage_ModifyTooltip { get; } = "Settings_General_AppearanceAndAction_AppFileStorage_ModifyTooltip";
            public static string Settings_General_AppearanceAndAction_AppFileStorage_OpenTooltip { get; } = "Settings_General_AppearanceAndAction_AppFileStorage_OpenTooltip";
            public static string Settings_General_AppearanceAndAction_AppFileStorageExplain { get; } = "Settings_General_AppearanceAndAction_AppFileStorageExplain";
            public static string Settings_General_AppearanceAndAction_AppLanguage { get; } = "Settings_General_AppearanceAndAction_AppLanguage";
            public static string Settings_General_AppearanceAndAction_AppLanguageExplain { get; } = "Settings_General_AppearanceAndAction_AppLanguageExplain";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdrop { get; } = "Settings_General_AppearanceAndAction_AppSystemBackdrop";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink { get; } = "Settings_General_AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink { get; } = "Settings_General_AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdropExplain { get; } = "Settings_General_AppearanceAndAction_AppSystemBackdropExplain";
            public static string Settings_General_AppearanceAndAction_AppTheme { get; } = "Settings_General_AppearanceAndAction_AppTheme";
            public static string Settings_General_AppearanceAndAction_AppThemeExplain { get; } = "Settings_General_AppearanceAndAction_AppThemeExplain";
            public static string Settings_General_AppearanceAndAction_AppThemeHyperlink { get; } = "Settings_General_AppearanceAndAction_AppThemeHyperlink";
            public static string Settings_General_AppearanceAndAction_AutoStart { get; } = "Settings_General_AppearanceAndAction_AutoStart";
            public static string Settings_General_AppearanceAndAction_AutoStatExplain { get; } = "Settings_General_AppearanceAndAction_AutoStatExplain";
            public static string Settings_General_Text_AppearanceAndAction { get; } = "Settings_General_Text_AppearanceAndAction";
            public static string Settings_General_Text_Version { get; } = "Settings_General_Text_Version";
            public static string Settings_General_Version_Download { get; } = "Settings_General_Version_Download";
            public static string Settings_General_Version_DownloadCancel { get; } = "Settings_General_Version_DownloadCancel";
            public static string Settings_General_Version_DownloadStart { get; } = "Settings_General_Version_DownloadStart";
            public static string Settings_General_Version_FindNew { get; } = "Settings_General_Version_FindNew";
            public static string Settings_General_Version_Install { get; } = "Settings_General_Version_Install";
            public static string Settings_General_Version_LastCheckDate { get; } = "Settings_General_Version_LastCheckDate";
            public static string Settings_General_Version_MsStore { get; } = "Settings_General_Version_MsStore";
            public static string Settings_General_Version_NewVersionDownLoaded { get; } = "Settings_General_Version_NewVersionDownLoaded";
            public static string Settings_General_Version_Release_Notes { get; } = "Settings_General_Version_Release_Notes";
            public static string Settings_General_Version_SeeNews { get; } = "Settings_General_Version_SeeNews";
            public static string Settings_General_Version_UpdateCheck { get; } = "Settings_General_Version_UpdateCheck";
            public static string Settings_General_Version_UpdateErr { get; } = "Settings_General_Version_UpdateErr";
            public static string Settings_General_Version_UptoNewest { get; } = "Settings_General_Version_UptoNewest";
            public static string Settings_Others_About_Basic { get; } = "Settings_Others_About_Basic";
            public static string Settings_Others_More_Document { get; } = "Settings_Others_More_Document";
            public static string Settings_Others_More_DocumentExplain { get; } = "Settings_Others_More_DocumentExplain";
            public static string Settings_Others_More_ReportBug { get; } = "Settings_Others_More_ReportBug";
            public static string Settings_Others_More_ReportBugExplain { get; } = "Settings_Others_More_ReportBugExplain";
            public static string Settings_Others_More_RequestFunc { get; } = "Settings_Others_More_RequestFunc";
            public static string Settings_Others_More_RequestFuncExplain { get; } = "Settings_Others_More_RequestFuncExplain";
            public static string Settings_Others_More_SourceCode { get; } = "Settings_Others_More_SourceCode";
            public static string Settings_Others_More_SourceCodeExplain { get; } = "Settings_Others_More_SourceCodeExplain";
            public static string Settings_Others_Text_About { get; } = "Settings_Others_Text_About";
            public static string Settings_Others_Text_More { get; } = "Settings_Others_Text_More";
            public static string Settings_Perforemance_Laptop_BatteryPoweredn { get; } = "Settings_Perforemance_Laptop_BatteryPoweredn";
            public static string Settings_Perforemance_Laptop_BatteryPowerednExplain { get; } = "Settings_Perforemance_Laptop_BatteryPowerednExplain";
            public static string Settings_Perforemance_Laptop_PowerSaving { get; } = "Settings_Perforemance_Laptop_PowerSaving";
            public static string Settings_Perforemance_Laptop_PowerSavingExplain { get; } = "Settings_Perforemance_Laptop_PowerSavingExplain";
            public static string Settings_Perforemance_Play__playStatu_KeepRun { get; } = "Settings_Perforemance_Play__playStatu_KeepRun";
            public static string Settings_Perforemance_Play__playStatu_Pause { get; } = "Settings_Perforemance_Play__playStatu_Pause";
            public static string Settings_Perforemance_Play__playStatu_Silence { get; } = "Settings_Perforemance_Play__playStatu_Silence";
            public static string Settings_Perforemance_Play_Audio { get; } = "Settings_Perforemance_Play_Audio";
            public static string Settings_Perforemance_Play_OthersFocus { get; } = "Settings_Perforemance_Play_OthersFocus";
            public static string Settings_Perforemance_Play_OthersFocusExplain { get; } = "Settings_Perforemance_Play_OthersFocusExplain";
            public static string Settings_Perforemance_Play_OthersFullScreen { get; } = "Settings_Perforemance_Play_OthersFullScreen";
            public static string Settings_Perforemance_Play_OthersFullScreenExplain { get; } = "Settings_Perforemance_Play_OthersFullScreenExplain";
            public static string Settings_Perforemance_System__statuMechanism_all { get; } = "Settings_Perforemance_System__statuMechanism_all";
            public static string Settings_Perforemance_System__statuMechanism_per { get; } = "Settings_Perforemance_System__statuMechanism_per";
            public static string Settings_Perforemance_System_RemoteDesktop { get; } = "Settings_Perforemance_System_RemoteDesktop";
            public static string Settings_Perforemance_System_RemoteDesktopExplain { get; } = "Settings_Perforemance_System_RemoteDesktopExplain";
            public static string Settings_Perforemance_System_StatuMechanism { get; } = "Settings_Perforemance_System_StatuMechanism";
            public static string Settings_Perforemance_System_StatuMechanismExplain_ForAll { get; } = "Settings_Perforemance_System_StatuMechanismExplain_ForAll";
            public static string Settings_Perforemance_System_StatuMechanismExplain_ForPer { get; } = "Settings_Perforemance_System_StatuMechanismExplain_ForPer";
            public static string Settings_Perforemance_Text_Laptop { get; } = "Settings_Perforemance_Text_Laptop";
            public static string Settings_Perforemance_Text_Play { get; } = "Settings_Perforemance_Text_Play";
            public static string Settings_Perforemance_Text_System { get; } = "Settings_Perforemance_Text_System";
            public static string Settings_System_Developer_Debug { get; } = "Settings_System_Developer_Debug";
            public static string Settings_System_Developer_DebugExplain { get; } = "Settings_System_Developer_DebugExplain";
            public static string Settings_System_Developer_Log { get; } = "Settings_System_Developer_Log";
            public static string Settings_System_Developer_LogExplain { get; } = "Settings_System_Developer_LogExplain";
            public static string Settings_System_Log { get; } = "Settings_System_Log";
            public static string Settings_System_Text_Debug { get; } = "Settings_System_Text_Debug";
            public static string Settings_System_Text_Developer { get; } = "Settings_System_Text_Developer";
            public static string SidebarAccount { get; } = "SidebarAccount";
            public static string SidebarAppSettings { get; } = "SidebarAppSettings";
            public static string SidebarGallery { get; } = "SidebarGallery";
            public static string SidebarProject { get; } = "SidebarProject";
            public static string SidebarWpSettings { get; } = "SidebarWpSettings";
            public static string Text_Adjust { get; } = "Text_Adjust";
            public static string Text_Apply { get; } = "Text_Apply";
            public static string Text_ApplyToLockBG { get; } = "Text_ApplyToLockBG";
            public static string Text_AspectRatio { get; } = "Text_AspectRatio";
            public static string Text_Cancel { get; } = "Text_Cancel";
            public static string Text_Close { get; } = "Text_Close";
            public static string Text_Confirm { get; } = "Text_Confirm";
            public static string Text_Delete { get; } = "Text_Delete";
            public static string Text_DeleteFromDisk { get; } = "Text_DeleteFromDisk";
            public static string Text_Details { get; } = "Text_Details";
            public static string Text_Detect { get; } = "Text_Detect";
            public static string Text_Edit { get; } = "Text_Edit";
            public static string Text_Edit_Desc { get; } = "Text_Edit_Desc";
            public static string Text_Edit_Tags { get; } = "Text_Edit_Tags";
            public static string Text_Edit_Title { get; } = "Text_Edit_Title";
            public static string Text_EffectConfig { get; } = "Text_EffectConfig";
            public static string Text_FileExtension { get; } = "Text_FileExtension";
            public static string Text_FileSize { get; } = "Text_FileSize";
            public static string Text_FileUsing { get; } = "Text_FileUsing";
            public static string Text_Identify { get; } = "Text_Identify";
            public static string Text_Loading { get; } = "Text_Loading";
            public static string Text_Off { get; } = "Text_Off";
            public static string Text_On { get; } = "Text_On";
            public static string Text_Preview { get; } = "Text_Preview";
            public static string Text_Resolution { get; } = "Text_Resolution";
            public static string Text_Restore { get; } = "Text_Restore";
            public static string Text_SaveAndApply { get; } = "Text_SaveAndApply";
            public static string Text_ShowOnDisk { get; } = "Text_ShowOnDisk";
            public static string Text_Type { get; } = "Text_Type";
            public static string Text_UpdateConfig { get; } = "Text_UpdateConfig";
            public static string Text_VersionInfo { get; } = "Text_VersionInfo";
            public static string Text_WpArrange { get; } = "Text_WpArrange";
            public static string WpArrange_Duplicate { get; } = "WpArrange_Duplicate";
            public static string WpArrange_DuplicateExplain { get; } = "WpArrange_DuplicateExplain";
            public static string WpArrange_Expand { get; } = "WpArrange_Expand";
            public static string WpArrange_ExpandExplain { get; } = "WpArrange_ExpandExplain";
            public static string WpArrange_Per { get; } = "WpArrange_Per";
            public static string WpArrange_PerExplain { get; } = "WpArrange_PerExplain";
            public static string WpCreateDialog_AIWp_Explain { get; } = "WpCreateDialog_AIWp_Explain";
            public static string WpCreateDialog_AIWp_Title { get; } = "WpCreateDialog_AIWp_Title";
            public static string WpCreateDialog_CommonWp_Explain { get; } = "WpCreateDialog_CommonWp_Explain";
            public static string WpCreateDialog_CommonWp_Title { get; } = "WpCreateDialog_CommonWp_Title";
            public static string WpSettings_NavTitle_LibraryContents { get; } = "WpSettings_NavTitle_LibraryContents";
            public static string WpSettings_NavTitle_ScrSettings { get; } = "WpSettings_NavTitle_ScrSettings";
            public static string? Project_RecentUsed { get; }
            public static string? Project_StartUp { get; }
            public static string? Project_SearchRecentUsed { get; }
            public static string Project_StartUp_OpenVsd { get; } = "Project_StartUp_OpenVsd";
            public static string Project_StartUp_OpenVsd_Desc { get; } = "Project_StartUp_OpenVsd_Desc";
            public static string Project_StartUp_OpenFile { get; } = "Project_StartUp_OpenFile";
            public static string Project_StartUp_OpenFile_Desc { get; } = "Project_StartUp_OpenFile_Desc";
            public static string Project_StartUp_OpenFolder { get; } = "Project_StartUp_OpenFolder";
            public static string Project_StartUp_OpenFolder_Desc { get; } = "Project_StartUp_OpenFolder_Desc";
            public static string Project_StartUp_NewVpd { get; } = "Project_StartUp_NewVpd";
            public static string Project_StartUp_NewVpd_Desc { get; } = "Project_StartUp_NewVpd_Desc";
            public static string? Project_ContinueWithoutFile { get; }
            public static string? Project_DeployNewDraft { get; }
            public static string? Project_NewDraftName { get; }
            public static string? Project_NewDraftName_Placeholder { get; }
            public static string? Project_NewDraftPosition { get; }
            public static string? Project_NewDraftPosition_BrowserFolder_Tooltip { get; }
            public static string? Project_DeployNewDraft_PreviousStep { get; }
            public static string? Project_DeployNewDraft_Create { get; }
            public static string? Project_DeployNewDraft_Desc { get; }
            public static string? Project_TemplateConfig { get; }
            public static string? Project_SearchTemplate { get; }
            public static string? Project_DeployNewDraft_NextStep { get; }
            public static string? Project_NewProjectName { get; }
            public static string? Project_NewProjectName_Placeholder { get; }
            public static string? Project_NewName_InvalidTip { get; }
            public static string? Project_NewPosition_InvalidTip { get; }
            public static string? DirExsits { get; }
        }

        public static class Field {
            public static int Cooldown { get; } = 3000; // ms
            public static string ThumGifSuff { get; } = "_thum.gif";
            public static string WpBasicDataFileName { get; } = "wp_metadata_basic.json";
            public static string WpEffectFilePathTemplate { get; } = "wpEffectFilePathTemplate.json";
            public static string WpEffectFilePathTemporary { get; } = "wpEffectFilePathTemporary.json";
            public static string WpEffectFilePathUsing { get; } = "wpEffectFilePathUsing.json";
            public static string WpRuntimeDataFileName { get; } = "wp_metadata_runtime.json";
        }

        public static class ColorKey {
            public static string WindowCaptionForeground { get; } = "WindowCaptionForeground";
            public static string WindowCaptionForegroundDisabled { get; } = "WindowCaptionForegroundDisabled";
        }
    }
}
