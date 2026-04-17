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
            public static string UniqueAppUpdateUid => "Virtual:Update:WALLPAPERSYSTEM";
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
            public static string? Project_StaticImg_Text_LayerName { get; }
            public static string? Project_StaticImg_Text_LayerNew { get; }
            public static string? Project_StaticImg_Text_LayerCopy { get; }
            public static string? Project_Unsave_Intercept_Content { get; }
            public static string? Project_Unsave_Intercept_Title { get; }
            public static string? Project_Export_Success { get; }
            public static string? Project_Export_Failed { get; }
            public static string? Project_Export_PathNotBeNone { get; }
            public static string? Project_Export_InternalError { get; }
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
