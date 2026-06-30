namespace VirtualPaper.Common {
    public static class Constants {

        //public static string GetMemory(object o) // 获取引用类型的内存地址方法    
        //{
        //    GCHandle h = GCHandle.Alloc(o, GCHandleType.WeakTrackResurrection);

        //    IntPtr addr = GCHandle.ToIntPtr(h);

        //    return "0x" + addr.ToString("X");
        //}

        /// <summary>
        /// 避免在测试环境中使用真实的 AppData 目录，防止污染用户数据
        /// </summary>
        public static bool IsTestMode { get; set; } = false;

        public static class Runtime {
            public static nint MainWindowHwnd { get; set; }
        }

        public static class CommonPaths {
            public static string TestRootDir { get; set; } = Path.Combine(Path.GetTempPath(), $"VirtualPaper_Test_{Guid.NewGuid():N}");
            private static string RootDir =>
                IsTestMode
                    ? TestRootDir
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper");
            public static string LocalApplicationData =>
                IsTestMode
                    ? TestRootDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            /// <summary>
            /// 数据存储根目录
            /// </summary>
            public static string AppDataDir => RootDir;
            //public static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper");
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

            public static string InstallerCacheDir => Path.Combine(AppDataDir, "installer_cache");
            public static string PendingUpdatesDir => Path.Combine(AppDataDir, "pending_updates");
            public static string UpdateFlagPath => Path.Combine(PendingUpdatesDir, "update.flag");
            public static string UpdateBackupDir => Path.Combine(PendingUpdatesDir, "_backup");
            public static string RollbackNoticePath => Path.Combine(AppDataDir, "update_rollback_notice.json");

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
            public static string Shader => Path.Combine("Plugins", "Shaders");
            public static string ML => Path.Combine("Plugins", "ML");
            public static string ML_DepthEstimate => Path.Combine(ML, "DepthEstimate");
            public static string ML_DepthEstimate_AI_Models => Path.Combine(ML_DepthEstimate, "ai_models");
            public static string ML_StyleTransfer => Path.Combine(ML, "StyleTransfer");
            public static string ML_StyleTransfer_AI_Models => Path.Combine(ML_StyleTransfer, "ai_models");
            public static string ML_SuperResolution => Path.Combine(ML, "SuperResolution");
            public static string ML_SuperResolution_AI_Models => Path.Combine(ML_SuperResolution, "ai_models");
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
            public static string AppBuildFile => "app_build.json";
        }

        public static class EnviromentVarKey {
            public static string BaseDir => "VIRTUALPAPER_BASE_DIR";
        }

        public static class ApplicationType {
            public static bool IsTestBuild => false;
            public static bool IsMSIX => new DesktopBridge.Helpers().IsRunningAsUwp();
        }

        public static class I18n {
            public static string Dialog_Content_ApplyError => "Dialog_Content_ApplyError";
            public static string Dialog_Content_GetMonitorsAsync => "Dialog_Content_GetMonitorsAsync";
            public static string Dialog_Content_Import_Failed_Lib => "Dialog_Content_Import_Failed_Lib";
            public static string Dialog_Content_Import_NeedUpdate => "Dialog_Content_Import_NeedUpdate";
            public static string Dialog_Content_LibraryDelete => "Dialog_Content_LibraryDelete";
            public static string Dialog_Content_OnlyPictureAndGif => "Dialog_Content_OnlyPictureAndGif";
            public static string Dialog_Content_WallpaperDirectoryChangePathInvalid => "Dialog_Content_WallpaperDirectoryChangePathInvalid";
            public static string Dialog_Title_CreateType => "Dialog_Title_CreateType";
            public static string Dialog_Title_Prompt => "Dialog_Title_Prompt";
            public static string InfobarMsg_Cancel => "InfobarMsg_Cancel";
            public static string InfobarMsg_Err => "InfobarMsg_Err";
            public static string InfobarMsg_ImportErr => "InfobarMsg_ImportErr";
            public static string InfobarMsg_Success => "InfobarMsg_Success";
            public static string AppUpdater_RollbackMessage => "AppUpdater_RollbackMessage";
            public static string Settings_General_Version_Plugins => "Settings_General_Version_Plugins";
            public static string RestartUpdate_InvalidInfo => "RestartUpdate_InvalidInfo";
            public static string RestartUpdate_Starting => "RestartUpdate_Starting";
            public static string RestartUpdate_Completed => "RestartUpdate_Completed";
            public static string RestartUpdate_Failed => "RestartUpdate_Failed";
            public static string RestartUpdate_Stage_Downloading => "RestartUpdate_Stage_Downloading";
            public static string RestartUpdate_Stage_BackingUp => "RestartUpdate_Stage_BackingUp";
            public static string RestartUpdate_Stage_Replacing => "RestartUpdate_Stage_Replacing";
            public static string RestartUpdate_Stage_Completed => "RestartUpdate_Stage_Completed";
            public static string RestartUpdate_Stage_Failed => "RestartUpdate_Stage_Failed";
            public static string RestartUpdate_Close => "RestartUpdate_Close";
            public static string RestartUpdate_PostponeTip => "RestartUpdate_PostponeTip";
            public static string Find_New_Version_Restart => "Find_New_Version_Restart";
            public static string ScreenSaver__effectBubble => "ScreenSaver__effectBubble";
            public static string ScreenSaver__effectNone => "ScreenSaver__effectNone";
            public static string Settings_General_AppearanceAndAction__sysbdAcrylic => "Settings_General_AppearanceAndAction__sysbdAcrylic";
            public static string Settings_General_AppearanceAndAction__sysbdDefault => "Settings_General_AppearanceAndAction__sysbdDefault";
            public static string Settings_General_AppearanceAndAction__sysbdMica => "Settings_General_AppearanceAndAction__sysbdMica";
            public static string Settings_General_Version_LastCheckDate => "Settings_General_Version_LastCheckDate";
            public static string Settings_General_Version_MsStore => "Settings_General_Version_MsStore";
            public static string Settings_Perforemance_Play__playStatu_KeepRun => "Settings_Perforemance_Play__playStatu_KeepRun";
            public static string Settings_Perforemance_Play__playStatu_Pause => "Settings_Perforemance_Play__playStatu_Pause";
            public static string Settings_Perforemance_Play__playStatu_Silence => "Settings_Perforemance_Play__playStatu_Silence";
            public static string Settings_Perforemance_System__statuMechanism_all => "Settings_Perforemance_System__statuMechanism_all";
            public static string Settings_Perforemance_System__statuMechanism_per => "Settings_Perforemance_System__statuMechanism_per";
            public static string Text_AspectRatio => "Text_AspectRatio";
            public static string Text_Cancel => "Text_Cancel";
            public static string Text_Confirm => "Text_Confirm";
            public static string Text_FileExtension => "Text_FileExtension";
            public static string Text_FileSize => "Text_FileSize";
            public static string Text_FileUsing => "Text_FileUsing";
            public static string Text_FileInPreview => "Text_FileInPreview";
            public static string Text_Off => "Text_Off";
            public static string Text_On => "Text_On";
            public static string Text_Resolution => "Text_Resolution";
            public static string? Text_Save { get; }
            public static string? Text_Unsave { get; }
            public static string Text_VersionInfo => "Text_VersionInfo";
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
            public static string WpLib_TypeFilter_All => "WpLib_TypeFilter_All";
            public static string WpLib_TypeFilter_StaticImage => "WpLib_TypeFilter_StaticImage";
            public static string WpLib_TypeFilter_DynamicImage => "WpLib_TypeFilter_DynamicImage";
            public static string WpLib_TypeFilter_Video => "WpLib_TypeFilter_Video";
            public static string WpLib_TypeFilter_WebInteractive => "WpLib_TypeFilter_WebInteractive";
            public static string? Project_DeployNewDraft_PreviousStep { get; }
            public static string? Project_DeployNewDraft_NextStep { get; }
            public static string? Project_NewName_InvalidTip { get; }
            public static string? Draft_SI_LayerLocked { get; }
            public static string? Draft_SI_LayerNotAvailable { get; }
            public static string? SIG_Text_AddLayer { get; }
            public static string? SIG_Text_CopyLayer { get; }
            public static string? SIG_Text_RenameLayer { get; }
            public static string? SIG_Text_DeleteLayer { get; }
            public static string? Dialog_Title_Rename { get; }
            public static string? StaticImg_CanvasSizeInput_Illegal { get; }
            public static string? Project_SI_FileNotFound { get; }
            public static string? Project_Drops_Contains_Invalid_FIles { get; }
            public static string? Project_FileLoad_Failed { get; }
            public static string? Project_SI_Text_BackgroundLayer { get; }
            public static string? Project_FileLoad_FileCorrupted { get; }
            public static string? Project_StaticImg_Text_LayerNew { get; }
            public static string? Project_StaticImg_Text_LayerCopy { get; }
            public static string? Project_Unsave_Intercept_Content { get; }
            public static string? Project_Unsave_Intercept_Title { get; }
            public static string? Project_Export_Success { get; }
            public static string? Project_Export_Failed { get; }
            public static string? Project_Export_PathNotBeNone { get; }
            public static string? Project_Export_InternalError { get; }
            public static string? Project_SI_FileTypeMismatch { get; }
            public static string? EffectPrefix_ { get; set; }
            public static string? Intelligent_AddTask { get; }
            public static string? Intelligent_Style_Type_Custom { get; }
            public static string? Intelligent_Style_Type_Anime { get; }
            public static string? Intelligent_Style_Type_Cartoon { get; }
            public static string? Intelligent_Style_Type_Gongbi { get; }
            public static string? Intelligent_Style_Type_Pencil { get; }
            public static string? Intelligent_Style_Type_Watercolor { get; }
            public static string? Intelligent_Style_Type_InkWash { get; }
            public static string? Intelligent_Style_Type_OilPainting { get; }
            public static string? Intelligent_Style_Type_ColoredPencil { get; }
            public static string? Intelligent_Style_Type_OutlineDrawing { get; }
            public static string? Intelligent_Style_Type_Ukiyoe { get; }
            public static string? Text_Task_Status_Completed { get; }
            public static string? Text_Task_Status_Queue { get; }
            public static string? Text_Task_Status_Processing { get; }
            public static string? Text_Task_Status_Failed { get; }
            public static string? Text_Task_Status_Unknown { get; }
            public static string? Text_File_Not_Available { get; }
            public static string? Add_To_Lib_Success { get; }
            public static string? Intelligent_Enhance_QualityRestore { get; }
            public static string? Intelligent_Enhance_SuperResolution { get; }
            public static string? Text_Error_InvalidFile { get; }
            // ── CanvasEffect 分组标题 ──────────────────────────────
            public static string? Project_StaticImg_EffectGroup_Adjust { get; }
            public static string? Project_StaticImg_EffectGroup_Color { get; }
            public static string? Project_StaticImg_EffectGroup_Special { get; }
            public static string? Project_StaticImg_EffectGroup_Blend { get; }
            // ── CanvasEffect 效果名称 ─────────────────────────────
            public static string? Project_StaticImg_Text_Effect_GrayScale { get; }
            public static string? Project_StaticImg_Text_Effect_Invert { get; }
            public static string? Project_StaticImg_Text_Effect_Exposure { get; }
            public static string? Project_StaticImg_Text_Effect_Brightness { get; }
            public static string? Project_StaticImg_Text_Effect_Saturation { get; }
            public static string? Project_StaticImg_Text_Effect_Hue { get; }
            public static string? Project_StaticImg_Text_Effect_Contrast { get; }
            public static string? Project_StaticImg_Text_Effect_Temperature { get; }
            public static string? Project_StaticImg_Text_Effect_Highlights { get; }
            public static string? Project_StaticImg_Text_Effect_Sepia { get; }
            public static string? Project_StaticImg_Text_Effect_Pixelate { get; }
            public static string? Project_StaticImg_Text_Effect_Emboss { get; }
            public static string? Project_StaticImg_Text_Effect_Blur { get; }
            public static string? Project_StaticImg_Text_Effect_Sharpen { get; }
            public static string? Project_StaticImg_Text_Effect_Noise { get; }
            public static string? Project_StaticImg_Text_Effect_Vignette { get; }
            public static string? Project_StaticImg_Text_Effect_Glow { get; }
            public static string? Project_StaticImg_Text_Effect_Bloom { get; }
            public static string? Project_StaticImg_Text_Effect_Distort { get; }
            public static string? Project_StaticImg_Text_Effect_Multiply { get; }
            public static string? Project_StaticImg_Text_Effect_Screen { get; }
            public static string? Project_StaticImg_Text_Effect_Overlay { get; }
            public static string? Project_StaticImg_Text_Effect_SoftLight { get; }
            // CanvasEffect 效果描述
            public static string? Project_StaticImg_Text_EffectDesc_GrayScale { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Invert { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Exposure { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Brightness { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Saturation { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Hue { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Contrast { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Temperature { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Highlights { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Sepia { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Pixelate { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Emboss { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Blur { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Sharpen { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Noise { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Vignette { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Glow { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Bloom { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Distort { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Multiply { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Screen { get; }
            public static string? Project_StaticImg_Text_EffectDesc_Overlay { get; }
            public static string? Project_StaticImg_Text_EffectDesc_SoftLight { get; }
            public static string? Settings_General_Version_FindNew { get; }
            public static string? AppUpdater_SpeedText_Ready { get; }
            public static string? Settings_General_Version_DownloadStart { get; }
            public static string? Settings_General_Version_Install { get; }
            public static string? Settings_General_Version_InstallerReady { get; }
            public static string? Settings_General_Version_PluginsReady { get; }
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
    }
}
