namespace VirtualPaper.Common {
    public static class Constants {  

        //public static string GetMemory(object o) // 获取引用类型的内存地址方法    
        //{
        //    GCHandle h = GCHandle.Alloc(o, GCHandleType.WeakTrackResurrection);

        //    IntPtr addr = GCHandle.ToIntPtr(h);

        //    return "0x" + addr.ToString("X");
        //}

        public static class Runtime {
            public static nint MainWindowHwnd { get; set; }
        }

        public static class CommonPaths {
            /// <summary>
            /// 数据存储根目录
            /// </summary>
            public static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper");
            public static string CommonDataDir => Path.Combine(AppDataDir, "data");

            /// <summary>
            /// 日志
            /// </summary>
            public static string LogDir => Path.Combine(AppDataDir, "logs");
            public static string LogDirCore => Path.Combine(LogDir, "Core");
            public static string LogDirUI => Path.Combine(LogDir, "UI");

            /// <summary>
            /// 临时缓存（预览、临时更换）
            /// </summary>
            public static string TempDir => Path.Combine(AppDataDir, "temp");

            public static string ExeIconDir => Path.Combine(CommonDataDir, "exeIcon");

            /// <summary>
            /// 壁纸存储目录
            /// </summary>
            public static string LibraryDir => Path.Combine(AppDataDir, "Library");

            public static string AppRulesPath => Path.Combine(AppDataDir, "app_rules.json");
            public static string WallpaperLayoutPath => Path.Combine(AppDataDir, "wallpaper_layout.json");
            public static string UserSettingsPath => Path.Combine(AppDataDir, "user_settings.json");
            public static string RecentUsedPath => Path.Combine(AppDataDir, "recent_used.json");

            public static string TempWebView2Dir => Path.Combine(AppDataDir, "WebView2");
            public static string TempScrWebView2Dir => Path.Combine(AppDataDir, "ScrWebView2");

            private static class Legacy {
                public static string AppRulesPath => Path.Combine(AppDataDir, "AppRules.json");
                public static string WallpaperLayoutPath => Path.Combine(AppDataDir, "WallpaperLayout.json");
                public static string UserSettingsPath => Path.Combine(AppDataDir, "UserSettings.json");
                public static string RecentUsedPath => Path.Combine(AppDataDir, "RecentUseds.json");
            }

            public static void Migrate() {
                MigrateFile(Legacy.AppRulesPath, AppRulesPath);
                MigrateFile(Legacy.WallpaperLayoutPath, WallpaperLayoutPath);
                MigrateFile(Legacy.UserSettingsPath, UserSettingsPath);
                MigrateFile(Legacy.RecentUsedPath, RecentUsedPath);
            }

            private static void MigrateFile(string oldPath, string newPath) {
                try {
                    bool oldExists = Path.Exists(oldPath);
                    if (!oldExists) return;

                    Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                    File.Move(oldPath, newPath, true);
                    File.Delete(oldPath);
                }
                catch { }
            }
        }

        public static class FolderName {
            /// <summary>
            /// 壁纸文件存储目录（本地导入 + 云端下载） + 壁纸自定义配置文件存储目录
            /// </summary>
            public static string WpStoreFolderName => "wallpapers";
        }

        public static class WorkingDir {
            public static string Shader => Path.Combine(UI, "Shaders");
            public static string ML => Path.Combine("Models");
            public static string PlayerWeb => Path.Combine("Plugins", "PlayerWeb");
            //public static string PlayerWeb => Path.Combine("Plugins", "PlayerWeb", "win-x64");
            public static string ScrSaver => Path.Combine("Plugins", "ScrSaver");
            public static string UI => Path.Combine("Plugins", "UI");
            //public static string UI => Path.Combine("Plugins", "UI", "win-x64");
        }

        public static class ModuleName {
            public static string UIComponent => "VirtualPaper.UIComponent";
            public static string UI => "VirtualPaper.UI.exe";
            public static string ScrSaver => "VirtualPaper.ScreenSaver.exe";
            public static string PlayerWeb => "VirtualPaper.PlayerWeb.exe";
        }

        public static class CoreField {
            public static string AppName => "VirtualPaper";
            public static string FileVersion => "18";
            public static string DraftFileVersion => "D18";
            public static string GrpcPipeServerName => "Grpc_" + PipeServerName;
            public static string PipeServerName => UniqueAppUid + Environment.UserName;
            public static string UniqueAppUid => "Virtual:WALLPAPERSYSTEM";
            public static string UniqueAppUIUid => "Virtual:UI:WALLPAPERSYSTEM";
        }

        public static class EnviromentVarKey {
            public static string BaseDir => "VIRTUALPAPER_BASE_DIR";
        }

        public static class ApplicationType {
            public static bool IsTestBuild => false;
            public static bool IsMSIX => new DesktopBridge.Helpers().IsRunningAsUwp();
        }

        public static class I18n {
            public static string AppSettings_SelBarItem1_General => "AppSettings_SelBarItem1_General";
            public static string AppSettings_SelBarItem2_Performance => "AppSettings_SelBarItem2_Performance";
            public static string AppSettings_SelBarItem3_System => "AppSettings_SelBarItem3_System";
            public static string AppSettings_SelBarItem4_Others => "AppSettings_SelBarItem4_Others";
            public static string Dialog_Content_ApplyError => "Dialog_Content_ApplyError";
            public static string Dialog_Content_GetMonitorsAsync => "Dialog_Content_GetMonitorsAsync";
            public static string Dialog_Content_Import_Failed_Lib => "Dialog_Content_Import_Failed_Lib";
            public static string Dialog_Content_Import_NeedUpdate => "Dialog_Content_Import_NeedUpdate";
            public static string Dialog_Content_LibraryDelete => "Dialog_Content_LibraryDelete";
            public static string Dialog_Content_OnlyPictureAndGif => "Dialog_Content_OnlyPictureAndGif";
            public static string Dialog_Content_WallpaperDirectoryChangePathInvalid => "Dialog_Content_WallpaperDirectoryChangePathInvalid";
            public static string Dialog_Content_WpIsUsing => "Dialog_Content_WpIsUsing";
            public static string Dialog_Title_CreateType => "Dialog_Title_CreateType";
            public static string Dialog_Title_Prompt => "Dialog_Title_Prompt";
            public static string Effect_Help_Parallax => "Effect_Help_Parallax";
            public static string Effect_Help_Scaling => "Effect_Help_Scaling";
            public static string InfobarMsg_Cancel => "InfobarMsg_Cancel";
            public static string InfobarMsg_Err => "InfobarMsg_Err";
            public static string InfobarMsg_ImportErr => "InfobarMsg_ImportErr";
            public static string InfobarMsg_Success => "InfobarMsg_Success";
            public static string ScreenSaver__effectBubble => "ScreenSaver__effectBubble";
            public static string ScreenSaver__effectNone => "ScreenSaver__effectNone";
            public static string ScreenSaver_Add => "ScreenSaver_Add";
            public static string ScreenSaver_DynamicEffects => "ScreenSaver_DynamicEffects";
            public static string ScreenSaver_DynamicEffectsExplain => "ScreenSaver_DynamicEffectsExplain";
            public static string ScreenSaver_RunningLock => "ScreenSaver_RunningLock";
            public static string ScreenSaver_RunningLockExplain => "ScreenSaver_RunningLockExplain";
            public static string ScreenSaver_SeekFromList => "ScreenSaver_SeekFromList";
            public static string ScreenSaver_Server => "ScreenSaver_Server";
            public static string ScreenSaver_ServerExplain => "ScreenSaver_ServerExplain";
            public static string ScreenSaver_WaitingTime => "ScreenSaver_WaitingTime";
            public static string ScreenSaver_WhiteListExplain => "ScreenSaver_WhiteListExplain";
            public static string ScreenSaver_WhiteListTitle => "ScreenSaver_WhiteListTitle";
            public static string Settings_General_AppearanceAndAction__sysbdAcrylic => "Settings_General_AppearanceAndAction__sysbdAcrylic";
            public static string Settings_General_AppearanceAndAction__sysbdDefault => "Settings_General_AppearanceAndAction__sysbdDefault";
            public static string Settings_General_AppearanceAndAction__sysbdMica => "Settings_General_AppearanceAndAction__sysbdMica";
            public static string Settings_General_AppearanceAndAction__themeDark => "Settings_General_AppearanceAndAction__themeDark";
            public static string Settings_General_AppearanceAndAction__themeFollowSystem => "Settings_General_AppearanceAndAction__themeFollowSystem";
            public static string Settings_General_AppearanceAndAction__themeLight => "Settings_General_AppearanceAndAction__themeLight";
            public static string Settings_General_AppearanceAndAction_AppFileStorage => "Settings_General_AppearanceAndAction_AppFileStorage";
            public static string Settings_General_AppearanceAndAction_AppFileStorage_ModifyTooltip => "Settings_General_AppearanceAndAction_AppFileStorage_ModifyTooltip";
            public static string Settings_General_AppearanceAndAction_AppFileStorage_OpenTooltip => "Settings_General_AppearanceAndAction_AppFileStorage_OpenTooltip";
            public static string Settings_General_AppearanceAndAction_AppFileStorageExplain => "Settings_General_AppearanceAndAction_AppFileStorageExplain";
            public static string Settings_General_AppearanceAndAction_AppLanguage => "Settings_General_AppearanceAndAction_AppLanguage";
            public static string Settings_General_AppearanceAndAction_AppLanguageExplain => "Settings_General_AppearanceAndAction_AppLanguageExplain";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdrop => "Settings_General_AppearanceAndAction_AppSystemBackdrop";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink => "Settings_General_AppearanceAndAction_AppSystemBackdrop_Acrylic_Hyperlink";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink => "Settings_General_AppearanceAndAction_AppSystemBackdrop_Mica_Hyperlink";
            public static string Settings_General_AppearanceAndAction_AppSystemBackdropExplain => "Settings_General_AppearanceAndAction_AppSystemBackdropExplain";
            public static string Settings_General_AppearanceAndAction_AppTheme => "Settings_General_AppearanceAndAction_AppTheme";
            public static string Settings_General_AppearanceAndAction_AppThemeExplain => "Settings_General_AppearanceAndAction_AppThemeExplain";
            public static string Settings_General_AppearanceAndAction_AppThemeHyperlink => "Settings_General_AppearanceAndAction_AppThemeHyperlink";
            public static string Settings_General_AppearanceAndAction_AutoStart => "Settings_General_AppearanceAndAction_AutoStart";
            public static string Settings_General_AppearanceAndAction_AutoStatExplain => "Settings_General_AppearanceAndAction_AutoStatExplain";
            public static string Settings_General_Text_AppearanceAndAction => "Settings_General_Text_AppearanceAndAction";
            public static string Settings_General_Text_Version => "Settings_General_Text_Version";
            public static string Settings_General_Version_Download => "Settings_General_Version_Download";
            public static string Settings_General_Version_DownloadCancel => "Settings_General_Version_DownloadCancel";
            public static string Settings_General_Version_DownloadStart => "Settings_General_Version_DownloadStart";
            public static string Settings_General_Version_FindNew => "Settings_General_Version_FindNew";
            public static string Settings_General_Version_Install => "Settings_General_Version_Install";
            public static string Settings_General_Version_LastCheckDate => "Settings_General_Version_LastCheckDate";
            public static string Settings_General_Version_MsStore => "Settings_General_Version_MsStore";
            public static string Settings_General_Version_NewVersionDownLoadedTitle => "Settings_General_Version_NewVersionDownLoadedTitle";
            public static string Settings_General_Version_Release_Notes => "Settings_General_Version_Release_Notes";
            public static string Settings_General_Version_SeeNews => "Settings_General_Version_SeeNews";
            public static string Settings_General_Version_UpdateCheck => "Settings_General_Version_UpdateCheck";
            public static string Settings_General_Version_UpdateErr => "Settings_General_Version_UpdateErr";
            public static string Settings_General_Version_UptoNewest => "Settings_General_Version_UptoNewest";
            public static string Settings_General_Version_DownloadingTitle => "Settings_General_Version_DownloadingTitle";
            public static string Settings_General_Version_DownloadFailedTitle => "Settings_General_Version_DownloadFailedTitle";
            public static string Settings_General_Version_DownloadFailedMsg => "Settings_General_Version_DownloadFailedMsg";
            public static string Settings_General_Version_VerifyFailedTitle => "Settings_General_Version_VerifyFailedTitle";
            public static string Settings_General_Version_VerifyFailedMsg => "Settings_General_Version_VerifyFailedMsg";
            public static string Settings_General_Version_DownLoadedMsg => "Settings_General_Version_DownLoadedMsg";
            public static string Settings_Others_About_Basic => "Settings_Others_About_Basic";
            public static string Settings_Others_More_Document => "Settings_Others_More_Document";
            public static string Settings_Others_More_DocumentExplain => "Settings_Others_More_DocumentExplain";
            public static string Settings_Others_More_ReportBug => "Settings_Others_More_ReportBug";
            public static string Settings_Others_More_ReportBugExplain => "Settings_Others_More_ReportBugExplain";
            public static string Settings_Others_More_RequestFunc => "Settings_Others_More_RequestFunc";
            public static string Settings_Others_More_RequestFuncExplain => "Settings_Others_More_RequestFuncExplain";
            public static string Settings_Others_More_SourceCode => "Settings_Others_More_SourceCode";
            public static string Settings_Others_More_SourceCodeExplain => "Settings_Others_More_SourceCodeExplain";
            public static string Settings_Others_Text_About => "Settings_Others_Text_About";
            public static string Settings_Others_Text_More => "Settings_Others_Text_More";
            public static string Settings_Perforemance_Laptop_BatteryPoweredn => "Settings_Perforemance_Laptop_BatteryPoweredn";
            public static string Settings_Perforemance_Laptop_BatteryPowerednExplain => "Settings_Perforemance_Laptop_BatteryPowerednExplain";
            public static string Settings_Perforemance_Laptop_PowerSaving => "Settings_Perforemance_Laptop_PowerSaving";
            public static string Settings_Perforemance_Laptop_PowerSavingExplain => "Settings_Perforemance_Laptop_PowerSavingExplain";
            public static string Settings_Perforemance_Play__playStatu_KeepRun => "Settings_Perforemance_Play__playStatu_KeepRun";
            public static string Settings_Perforemance_Play__playStatu_Pause => "Settings_Perforemance_Play__playStatu_Pause";
            public static string Settings_Perforemance_Play__playStatu_Silence => "Settings_Perforemance_Play__playStatu_Silence";
            public static string Settings_Perforemance_Play_Audio => "Settings_Perforemance_Play_Audio";
            public static string Settings_Perforemance_Play_OthersFocus => "Settings_Perforemance_Play_OthersFocus";
            public static string Settings_Perforemance_Play_OthersFocusExplain => "Settings_Perforemance_Play_OthersFocusExplain";
            public static string Settings_Perforemance_Play_OthersFullScreen => "Settings_Perforemance_Play_OthersFullScreen";
            public static string Settings_Perforemance_Play_OthersFullScreenExplain => "Settings_Perforemance_Play_OthersFullScreenExplain";
            public static string Settings_Perforemance_System__statuMechanism_all => "Settings_Perforemance_System__statuMechanism_all";
            public static string Settings_Perforemance_System__statuMechanism_per => "Settings_Perforemance_System__statuMechanism_per";
            public static string Settings_Perforemance_System_RemoteDesktop => "Settings_Perforemance_System_RemoteDesktop";
            public static string Settings_Perforemance_System_RemoteDesktopExplain => "Settings_Perforemance_System_RemoteDesktopExplain";
            public static string Settings_Perforemance_System_StatuMechanism => "Settings_Perforemance_System_StatuMechanism";
            public static string Settings_Perforemance_System_StatuMechanismExplain_ForAll => "Settings_Perforemance_System_StatuMechanismExplain_ForAll";
            public static string Settings_Perforemance_System_StatuMechanismExplain_ForPer => "Settings_Perforemance_System_StatuMechanismExplain_ForPer";
            public static string Settings_Perforemance_Text_Laptop => "Settings_Perforemance_Text_Laptop";
            public static string Settings_Perforemance_Text_Play => "Settings_Perforemance_Text_Play";
            public static string Settings_Perforemance_Text_System => "Settings_Perforemance_Text_System";
            public static string Settings_System_Developer_Debug => "Settings_System_Developer_Debug";
            public static string Settings_System_Developer_DebugExplain => "Settings_System_Developer_DebugExplain";
            public static string Settings_System_Developer_Log => "Settings_System_Developer_Log";
            public static string Settings_System_Developer_LogExplain => "Settings_System_Developer_LogExplain";
            public static string Settings_System_Log => "Settings_System_Log";
            public static string Settings_System_Text_Debug => "Settings_System_Text_Debug";
            public static string Settings_System_Text_Developer => "Settings_System_Text_Developer";
            public static string SidebarAccount => "SidebarAccount";
            public static string SidebarAppSettings => "SidebarAppSettings";
            public static string SidebarGallery => "SidebarGallery";
            public static string SidebarProject => "SidebarProject";
            public static string SidebarRepository => "SidebarRepository";
            public static string Text_Adjust => "Text_Adjust";
            public static string Text_Apply => "Text_Apply";
            public static string Text_ApplyToLockBG => "Text_ApplyToLockBG";
            public static string Text_AspectRatio => "Text_AspectRatio";
            public static string Text_Cancel => "Text_Cancel";
            public static string Text_Close => "Text_Close";
            public static string Text_Confirm => "Text_Confirm";
            public static string Text_Delete => "Text_Delete";
            public static string Text_DeleteFromDisk => "Text_DeleteFromDisk";
            public static string Text_Details => "Text_Details";
            public static string Text_Detect => "Text_Detect";
            public static string Text_Edit => "Text_Edit";
            public static string Text_Edit_Desc => "Text_Edit_Desc";
            public static string Text_Edit_Tags => "Text_Edit_Tags";
            public static string Text_Edit_Title => "Text_Edit_Title";
            public static string Text_EffectConfig => "Text_EffectConfig";
            public static string Text_FileExtension => "Text_FileExtension";
            public static string Text_FileSize => "Text_FileSize";
            public static string Text_FileUsing => "Text_FileUsing";
            public static string Text_FileInPreview => "Text_FileInPreview";
            public static string Text_Identify => "Text_Identify";
            public static string Text_Loading => "Text_Loading";
            public static string Text_Off => "Text_Off";
            public static string Text_On => "Text_On";
            public static string Text_Preview => "Text_Preview";
            public static string Text_Resolution => "Text_Resolution";
            public static string Text_Restore => "Text_Restore";
            public static string Text_SaveAndApply => "Text_SaveAndApply";
            public static string Text_ShowOnDisk => "Text_ShowOnDisk";
            public static string Text_Type => "Text_Type";
            public static string Text_UpdateConfig => "Text_UpdateConfig";
            public static string Text_VersionInfo => "Text_VersionInfo";
            public static string Text_WpArrange => "Text_WpArrange";
            public static string WpArrange_Duplicate => "WpArrange_Duplicate";
            public static string WpArrange_DuplicateExplain => "WpArrange_DuplicateExplain";
            public static string WpArrange_Expand => "WpArrange_Expand";
            public static string WpArrange_ExpandExplain => "WpArrange_ExpandExplain";
            public static string WpArrange_Per => "WpArrange_Per";
            public static string WpArrange_PerExplain => "WpArrange_PerExplain";
            public static string WpCreateDialog_AIWp_Explain => "WpCreateDialog_AIWp_Explain";
            public static string WpCreateDialog_AIWp_Title => "WpCreateDialog_AIWp_Title";
            public static string WpCreateDialog_CommonWp_Explain => "WpCreateDialog_CommonWp_Explain";
            public static string WpCreateDialog_CommonWp_Title => "WpCreateDialog_CommonWp_Title";
            public static string WpSettings_NavTitle_LibraryContents => "WpSettings_NavTitle_LibraryContents";
            public static string WpSettings_NavTitle_ScrSettings => "WpSettings_NavTitle_ScrSettings";
            public static string? Project_RecentUsed { get; }
            public static string? Project_StartUp { get; }
            public static string? Project_SearchRecentUsed { get; }
            public static string Project_StartUp_OpenVsd => "Project_StartUp_OpenVsd";
            public static string Project_StartUp_OpenVsd_Desc => "Project_StartUp_OpenVsd_Desc";
            public static string Project_StartUp_OpenFile => "Project_StartUp_OpenFile";
            public static string Project_StartUp_OpenFile_Desc => "Project_StartUp_OpenFile_Desc";
            public static string Project_StartUp_OpenFolder => "Project_StartUp_OpenFolder";
            public static string Project_StartUp_OpenFolder_Desc => "Project_StartUp_OpenFolder_Desc";
            public static string Project_StartUp_NewVpd => "Project_StartUp_NewVpd";
            public static string Project_StartUp_NewVpd_Desc => "Project_StartUp_NewVpd_Desc";
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
            public static string? Draft_SI_LayerLocked { get; }
            public static string? Draft_Canvas_DefaultName { get; }
            public static string? Draft_SI_LayerNotAvailable { get; }
            public static string? SIG_Text_AddLayer { get; }
            public static string? SIG_Text_CopyLayer { get; }
            public static string? SIG_Text_RenameLayer { get; }
            public static string? SIG_Text_DeleteLayer { get; }
            public static string? RenameDialog_Text_AfterChange { get; }
            public static string? RenameDialog_Text_BeforeChange { get; }
            public static string? Dialog_Title_Rename { get; }
            public static string? Project_CannotDelete_OnlyCanvas { get; }
            public static string? Project_STI_LayerSaveFailed { get; }
            public static string? Project_STI_LayerLoadFailed { get; }
            public static string? StaticImg_CanvasSizeInput_Illegal { get; }
            public static string? Account_Email_InvalidTip { get; }
            public static string? Account_Pwd_InvalidTip { get; }
            public static string? Account_LoginWithAccount { get; }
            public static string? Account_EmailText { get; }
            public static string? Account_EmailTextWithColon { get; }
            public static string? Account_PwdText { get; }
            public static string? Account_PwdTextWithColon { get; }
            public static string? Account_RegisterWithEmail { get; }
            public static string? Account_ConfirmPwdText { get; }
            public static string? Account_ConfirmPwdTextWithColon { get; }
            public static string? Account_CodeText { get; }
            public static string? Account_CodeTextWithColon { get; }
            public static string? Account_UsernameText { get; }
            public static string? Account_UsernameTextWithColon { get; }
            public static string? Account_LoginText { get; }
            public static string? Account_RegisterText { get; }
            public static string? Account_BackText { get; }
            public static string? Account_RequestCodeText { get; }
            public static string? Account_Username_InvalidTip { get; }
            public static string? Server_CannotAccess { get; }
            public static string? Account_ConfirmPwd_InvalidTip { get; }
            public static string? Account_SelBarItem1_Cloud { get; set; }
            public static string? Account_SelBarItem2_Star { get; set; }
            public static string? Account_SelBarItem3_Upload { get; set; }
            public static string? InnerErr { get; }
            public static string? Account_DefaultSign { get; }
            public static string? Account_AvatarDesc { get; }
            public static string? Account_ChangeAvatar { get; }
            public static string? Account_Username { get; }
            public static string? Account_Sign { get; }
            public static string? Account_UserAccount { get; }
            public static string? Account_PersonalInfo { get; }
            public static string? Account_Email { get; }
            public static string? Account_Sign_InvalidTip { get; }
            public static string? Text_FileNotAccess { get; }
            public static string? Text_FileSizeIllegal_5MB { get; }
            public static string? Account_Username_InvalidTip_Sim { get; }
            public static string? Account_Usersign_InvalidTip_Sim { get; }
            public static string? Account_UpdateUserInfo_Fail { get; }
            public static string? Account_Logout { get; }
            public static string? MenuFlyout_Text_DetailAndEditInfo { get; }
            public static string? MenuFlyout_Text_Downlaod { get; }
            public static string? App_UserNotLogin { get; }
            public static string? Text_DeleteFromServer { get; }
            public static string? SIG_CanvasSet_Header { get; }
            public static string? SIG_CanvasSet_AdjustSize { get; }
            public static string? SIG_CanvasSet_PixelWidth { get; }
            public static string? SIG_CanvasSet_PixelHeight { get; }
            public static string? SIG_CanvasSet_LockAspectRatio { get; }
            public static string? SIG_CanvasSet_SacleContent { get; }
            public static string? SIG_CanvasSet_RotateAndFlip { get; }
            public static string? SIG_CanvasSet_RotateLeftNinety { get; }
            public static string? SIG_CanvasSet_RotateRightNinety { get; }
            public static string? SIG_CanvasSet_FlipHorizon { get; }
            public static string? SIG_CanvasSet_FlipVertical { get; }
            public static string? Project_SI_Text_Layer { get; }
            public static string? Project_SI_Text_Copy { get; }
            public static string? Project_SI_Text_UnnamedLayer { get; }
            public static string? Project_SI_FileNotFound { get; }
            public static string? SIG_Text_RemoveFromList { get; }
            public static string? SIG_Text_CopyPath { get; }
            public static string? RunningAsAdminWarning { get; }
            public static string? Project_Drops_Contains_Invalid_FIles { get; }
        }

        public static class Field {
            public static int Cooldown => 3000; // ms
            public static string ThumGifSuff => "_thum.gif";
            public static string WpBasicDataFileName => "wp_metadata_basic.json";
            public static string WpEffectFilePathTemplate => "wpEffectFilePathTemplate.json";
            public static string WpEffectFilePathTemporary => "wpEffectFilePathTemporary.json";
            public static string WpEffectFilePathUsing => "wpEffectFilePathUsing.json";
            public static string WpRuntimeDataFileName => "wp_metadata_runtime.json";
        }

        public static class ColorKey {
            public static string WindowCaptionForeground => "WindowCaptionForeground";
            public static string WindowCaptionForegroundDisabled => "WindowCaptionForegroundDisabled";
        }
    }
}
