namespace VirtualPaper.Common {
    public static class Constants {

        //public static string GetMemory(object o) // 获取引用类型的内存地址方法    
        //{
        //    GCHandle h = GCHandle.Alloc(o, GCHandleType.WeakTrackResurrection);

        //    IntPtr addr = GCHandle.ToIntPtr(h);

        //    return "0x" + addr.ToString("X");
        //}

        /// <summary>
        /// 单元测试使用替代行为。真实进程测试仅隔离数据目录，不启用这些替代行为。
        /// </summary>
        public static bool IsTestMode { get; set; }

        public static class Runtime {
            public static nint MainWindowHwnd { get; set; }
        }

        public static class CommonPaths {
            public static string TestRootDir { get; set; } = TestProcessEnvironment.DataRoot ?? Path.Combine(Path.GetTempPath(), $"VirtualPaper_Test_{Guid.NewGuid():N}");
            private static string RootDir => TestProcessEnvironment.DataRoot ?? (
                IsTestMode
                    ? TestRootDir
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualPaper"));
            public static string LocalApplicationData => TestProcessEnvironment.DataRoot ?? (
                IsTestMode
                    ? TestRootDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

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
            public static string TempStaticImgUndoDir => Path.Combine(TempDir, "staticImgUndo");

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

            public static string PendingPluginsUpdateDir => Path.Combine(AppDataDir, "pending_plugins_update");
            public static string PendingInstallerUpdateDir => Path.Combine(AppDataDir, "pending_installer_update");
            public static string UpdateFlagPath => Path.Combine(PendingPluginsUpdateDir, "update.flag");
            public static string InstallerUpdateFlagPath => Path.Combine(PendingInstallerUpdateDir, "update.flag");
            public static string UpdateBackupDir => Path.Combine(PendingPluginsUpdateDir, "_backup");
            public static string PluginPatchExtractDir => Path.Combine(PendingPluginsUpdateDir, "extracted");
            public static string UpdateFailedNoticePath => Path.Combine(AppDataDir, "update_failed_notice.json");

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
            public static string WebBackdrop => "WebBackdrop";
        }

        public static class CoreField {
            public static string AppName => "VirtualPaper";
            public static string FileVersion => "19";
            public static string DraftFileVersion => "D18";
            public static string GrpcPipeServerName => "Grpc_" + PipeServerName;
            public static string PipeServerName => UniqueAppUid + Environment.UserName;
            public static string UniqueAppUid => "Virtual:WALLPAPERSYSTEM" + TestSessionSuffix;
            public static string UniqueAppUIUid => "Virtual:UI:WALLPAPERSYSTEM" + TestSessionSuffix;
            private static string TestSessionSuffix => TestProcessEnvironment.IsEnabled ? ":" + TestProcessEnvironment.SessionId : string.Empty;
            public static string AppBuildFile => "app_build.json";
            public const string InstallerLanguageFileName = "installer-language.txt";
        }

        public static class PipeControlField {
            public static string TrayCmdPipeName => "TRAY_CMD" +
                (TestProcessEnvironment.IsEnabled ? ":" + TestProcessEnvironment.SessionId : string.Empty);
            public static string CmdUpdateScrSettings => "UPDATE_SCRSETTINGS";
        }

        public static class EnviromentVarKey {
            public static string BaseDir => "VIRTUALPAPER_BASE_DIR";
        }

        public static class ApplicationType {
            public static bool IsTestBuild => false;
            public static bool IsMSIX => new DesktopBridge.Helpers().IsRunningAsUwp();
        }

        public static class I18n {
            public const string Dialog_Content_ApplyError = nameof(Dialog_Content_ApplyError);
            public const string Dialog_Content_GetMonitorsAsync = nameof(Dialog_Content_GetMonitorsAsync);
            public const string Dialog_Content_Import_Failed_Lib = nameof(Dialog_Content_Import_Failed_Lib);
            public const string Dialog_Content_Import_NeedUpdate = nameof(Dialog_Content_Import_NeedUpdate);
            public const string Dialog_Content_LibraryDelete = nameof(Dialog_Content_LibraryDelete);
            public const string Dialog_Content_LibraryDeleteMany = nameof(Dialog_Content_LibraryDeleteMany);
            public const string Dialog_Content_OnlyPictureAndGif = nameof(Dialog_Content_OnlyPictureAndGif);
            public const string Dialog_Content_WallpaperDirectoryChangePathInvalid = nameof(Dialog_Content_WallpaperDirectoryChangePathInvalid);
            public const string Dialog_Title_CreateType = nameof(Dialog_Title_CreateType);
            public const string Dialog_Title_LibraryDelete = nameof(Dialog_Title_LibraryDelete);
            public const string Dialog_Title_LibraryDeleteMany = nameof(Dialog_Title_LibraryDeleteMany);
            public const string Dialog_Title_LibraryUpgrade = nameof(Dialog_Title_LibraryUpgrade);
            public const string Dialog_Title_RecentProjectMissing = nameof(Dialog_Title_RecentProjectMissing);
            public const string Dialog_Title_Prompt = nameof(Dialog_Title_Prompt);
            public const string InfobarMsg_Cancel = nameof(InfobarMsg_Cancel);
            public const string InfobarMsg_Err = nameof(InfobarMsg_Err);
            public const string InfobarMsg_ImportErr = nameof(InfobarMsg_ImportErr);
            public const string InfobarMsg_Success = nameof(InfobarMsg_Success);
            public const string AppUpdater_UpdateFailedMessage = nameof(AppUpdater_UpdateFailedMessage);
            public const string Settings_General_Version_Plugins = nameof(Settings_General_Version_Plugins);
            public const string PluginsUpdate_InvalidInfo = nameof(PluginsUpdate_InvalidInfo);
            public const string PluginsUpdate_Starting = nameof(PluginsUpdate_Starting);
            public const string PluginsUpdate_Completed = nameof(PluginsUpdate_Completed);
            public const string PluginsUpdate_Failed = nameof(PluginsUpdate_Failed);
            public const string PluginsUpdate_Stage_Downloading = nameof(PluginsUpdate_Stage_Downloading);
            public const string PluginsUpdate_Stage_BackingUp = nameof(PluginsUpdate_Stage_BackingUp);
            public const string PluginsUpdate_Stage_Replacing = nameof(PluginsUpdate_Stage_Replacing);
            public const string PluginsUpdate_Stage_Completed = nameof(PluginsUpdate_Stage_Completed);
            public const string PluginsUpdate_Stage_Failed = nameof(PluginsUpdate_Stage_Failed);
            public const string PluginsUpdate_Close = nameof(PluginsUpdate_Close);
            public const string PluginsUpdate_PostponeTip = nameof(PluginsUpdate_PostponeTip);
            public const string Find_New_Version_Restart = nameof(Find_New_Version_Restart);
            public const string ScreenSaver__effectBubble = nameof(ScreenSaver__effectBubble);
            public const string ScreenSaver__effectNone = nameof(ScreenSaver__effectNone);
            public const string Settings_General_AppearanceAndAction__sysbdAcrylic = nameof(Settings_General_AppearanceAndAction__sysbdAcrylic);
            public const string Settings_General_AppearanceAndAction__sysbdDefault = nameof(Settings_General_AppearanceAndAction__sysbdDefault);
            public const string Settings_General_AppearanceAndAction__sysbdMica = nameof(Settings_General_AppearanceAndAction__sysbdMica);
            public const string Settings_General_Version_LastCheckDate = nameof(Settings_General_Version_LastCheckDate);
            public const string Settings_General_Version_MsStore = nameof(Settings_General_Version_MsStore);
            public const string Settings_Perforemance_Play__playStatu_KeepRun = nameof(Settings_Perforemance_Play__playStatu_KeepRun);
            public const string Settings_Perforemance_Play__playStatu_Pause = nameof(Settings_Perforemance_Play__playStatu_Pause);
            public const string Settings_Perforemance_Play__playStatu_Silence = nameof(Settings_Perforemance_Play__playStatu_Silence);
            public const string Settings_Perforemance_System__statuMechanism_all = nameof(Settings_Perforemance_System__statuMechanism_all);
            public const string Settings_Perforemance_System__statuMechanism_per = nameof(Settings_Perforemance_System__statuMechanism_per);
            public const string Text_AspectRatio = nameof(Text_AspectRatio);
            public const string Text_Cancel = nameof(Text_Cancel);
            public const string Text_Confirm = nameof(Text_Confirm);
            public const string Text_Adjust = nameof(Text_Adjust);
            public const string Text_Details = nameof(Text_Details);
            public const string PlayerWeb_DetailsWindowTitle = nameof(PlayerWeb_DetailsWindowTitle);
            public const string Text_Create = nameof(Text_Create);
            public const string Text_Delete = nameof(Text_Delete);
            public const string Text_RemoveRecord = nameof(Text_RemoveRecord);
            public const string Text_Rename = nameof(Text_Rename);
            public const string Text_Upgrade = nameof(Text_Upgrade);
            public const string Text_FileExtension = nameof(Text_FileExtension);
            public const string Text_FileSize = nameof(Text_FileSize);
            public const string Text_FileUsing = nameof(Text_FileUsing);
            public const string Text_FileInPreview = nameof(Text_FileInPreview);
            public const string Text_Off = nameof(Text_Off);
            public const string Text_On = nameof(Text_On);
            public const string Text_Resolution = nameof(Text_Resolution);
            public const string Text_ShowOnDisk = nameof(Text_ShowOnDisk);
            public const string Text_Save = nameof(Text_Save);
            public const string Text_Unsave = nameof(Text_Unsave);
            public const string Text_VersionInfo = nameof(Text_VersionInfo);
            public const string WpArrange_Duplicate = nameof(WpArrange_Duplicate);
            public const string WpLib_SelectedCount = nameof(WpLib_SelectedCount);
            public const string WpArrange_DuplicateExplain = nameof(WpArrange_DuplicateExplain);
            public const string WpArrange_Expand = nameof(WpArrange_Expand);
            public const string WpArrange_ExpandExplain = nameof(WpArrange_ExpandExplain);
            public const string WpArrange_Per = nameof(WpArrange_Per);
            public const string WpArrange_PerExplain = nameof(WpArrange_PerExplain);
            public const string WpCreateDialog_AIWp_Explain = nameof(WpCreateDialog_AIWp_Explain);
            public const string WpCreateDialog_AIWp_Title = nameof(WpCreateDialog_AIWp_Title);
            public const string WpCreateDialog_CommonWp_Explain = nameof(WpCreateDialog_CommonWp_Explain);
            public const string WpCreateDialog_CommonWp_Title = nameof(WpCreateDialog_CommonWp_Title);
            public const string WpLib_TypeFilter_All = nameof(WpLib_TypeFilter_All);
            public const string WpLib_TypeFilter_StaticImage = nameof(WpLib_TypeFilter_StaticImage);
            public const string WpLib_TypeFilter_DynamicImage = nameof(WpLib_TypeFilter_DynamicImage);
            public const string WpLib_TypeFilter_Video = nameof(WpLib_TypeFilter_Video);
            public const string WpLib_TypeFilter_WebInteractive = nameof(WpLib_TypeFilter_WebInteractive);
            public const string Project_DeployNewDraft_PreviousStep = nameof(Project_DeployNewDraft_PreviousStep);
            public const string Project_DeployNewDraft_NextStep = nameof(Project_DeployNewDraft_NextStep);
            public const string Project_NewName_InvalidTip = nameof(Project_NewName_InvalidTip);
            public const string Draft_SI_LayerLocked = nameof(Draft_SI_LayerLocked);
            public const string Draft_SI_LayerNotAvailable = nameof(Draft_SI_LayerNotAvailable);
            public const string SIG_Text_AddLayer = nameof(SIG_Text_AddLayer);
            public const string SIG_Text_CopyLayer = nameof(SIG_Text_CopyLayer);
            public const string SIG_Text_RenameLayer = nameof(SIG_Text_RenameLayer);
            public const string SIG_Text_DeleteLayer = nameof(SIG_Text_DeleteLayer);
            public const string Dialog_Title_Rename = nameof(Dialog_Title_Rename);
            public const string StaticImg_CanvasSizeInput_Illegal = nameof(StaticImg_CanvasSizeInput_Illegal);
            public const string Project_SI_FileNotFound = nameof(Project_SI_FileNotFound);
            public const string Project_Drops_Contains_Invalid_FIles = nameof(Project_Drops_Contains_Invalid_FIles);
            public const string Project_FileLoad_Failed = nameof(Project_FileLoad_Failed);
            public const string Project_SI_Text_BackgroundLayer = nameof(Project_SI_Text_BackgroundLayer);
            public const string Project_FileLoad_FileCorrupted = nameof(Project_FileLoad_FileCorrupted);
            public const string Project_StaticImg_Text_LayerNew = nameof(Project_StaticImg_Text_LayerNew);
            public const string Project_StaticImg_Text_Paintbrush = nameof(Project_StaticImg_Text_Paintbrush);
            public const string Intelligent_Task_Style_Img_Import = nameof(Intelligent_Task_Style_Img_Import);
            public const string Project_StaticImg_Text_LayerCopy = nameof(Project_StaticImg_Text_LayerCopy);
            public const string Project_Unsave_Intercept_Content = nameof(Project_Unsave_Intercept_Content);
            public const string Project_Unsave_Intercept_Title = nameof(Project_Unsave_Intercept_Title);
            public const string Project_Export_Success = nameof(Project_Export_Success);
            public const string Project_Export_Failed = nameof(Project_Export_Failed);
            public const string Project_Export_PathNotBeNone = nameof(Project_Export_PathNotBeNone);
            public const string Project_Export_InternalError = nameof(Project_Export_InternalError);
            public const string Project_SI_FileTypeMismatch = nameof(Project_SI_FileTypeMismatch);
            public const string EffectPrefix_ = nameof(EffectPrefix_);
            public const string Intelligent_AddTask = nameof(Intelligent_AddTask);
            public const string Intelligent_Style_Type_Custom = nameof(Intelligent_Style_Type_Custom);
            public const string Intelligent_Style_Type_Anime = nameof(Intelligent_Style_Type_Anime);
            public const string Intelligent_Style_Type_Cartoon = nameof(Intelligent_Style_Type_Cartoon);
            public const string Intelligent_Style_Type_Gongbi = nameof(Intelligent_Style_Type_Gongbi);
            public const string Intelligent_Style_Type_Pencil = nameof(Intelligent_Style_Type_Pencil);
            public const string Intelligent_Style_Type_Watercolor = nameof(Intelligent_Style_Type_Watercolor);
            public const string Intelligent_Style_Type_InkWash = nameof(Intelligent_Style_Type_InkWash);
            public const string Intelligent_Style_Type_OilPainting = nameof(Intelligent_Style_Type_OilPainting);
            public const string Intelligent_Style_Type_ColoredPencil = nameof(Intelligent_Style_Type_ColoredPencil);
            public const string Intelligent_Style_Type_OutlineDrawing = nameof(Intelligent_Style_Type_OutlineDrawing);
            public const string Intelligent_Style_Type_Ukiyoe = nameof(Intelligent_Style_Type_Ukiyoe);
            public const string Intelligent_Result_Temporary = nameof(Intelligent_Result_Temporary);
            public const string Intelligent_Result_Exported = nameof(Intelligent_Result_Exported);
            public const string Intelligent_Result_Imported = nameof(Intelligent_Result_Imported);
            public const string Intelligent_Result_ExportedAndImported = nameof(Intelligent_Result_ExportedAndImported);
            public const string Intelligent_Close_Title = nameof(Intelligent_Close_Title);
            public const string Intelligent_Close_Active = nameof(Intelligent_Close_Active);
            public const string Intelligent_Close_Temporary = nameof(Intelligent_Close_Temporary);
            public const string Intelligent_Close_DontRemind = nameof(Intelligent_Close_DontRemind);
            public const string Intelligent_Close_Return = nameof(Intelligent_Close_Return);
            public const string Intelligent_Close_Confirm = nameof(Intelligent_Close_Confirm);
            public const string Intelligent_Remove_Title = nameof(Intelligent_Remove_Title);
            public const string Intelligent_Remove_Temporary = nameof(Intelligent_Remove_Temporary);
            public const string Text_Task_Status_Completed = nameof(Text_Task_Status_Completed);
            public const string Text_Task_Status_Queue = nameof(Text_Task_Status_Queue);
            public const string Text_Task_Status_Processing = nameof(Text_Task_Status_Processing);
            public const string Text_Task_Status_Failed = nameof(Text_Task_Status_Failed);
            public const string Text_Task_Status_Unknown = nameof(Text_Task_Status_Unknown);
            public const string Text_File_Not_Available = nameof(Text_File_Not_Available);
            public const string Add_To_Lib_Success = nameof(Add_To_Lib_Success);
            public const string Intelligent_Enhance_QualityRestore = nameof(Intelligent_Enhance_QualityRestore);
            public const string Intelligent_Enhance_SuperResolution = nameof(Intelligent_Enhance_SuperResolution);
            public const string Text_Error_InvalidFile = nameof(Text_Error_InvalidFile);
            // ── CanvasEffect 分组标题 ──────────────────────────────
            public const string Project_StaticImg_EffectGroup_Adjust = nameof(Project_StaticImg_EffectGroup_Adjust);
            public const string Project_StaticImg_EffectGroup_Color = nameof(Project_StaticImg_EffectGroup_Color);
            public const string Project_StaticImg_EffectGroup_Special = nameof(Project_StaticImg_EffectGroup_Special);
            public const string Project_StaticImg_EffectGroup_Blend = nameof(Project_StaticImg_EffectGroup_Blend);
            // ── CanvasEffect 效果名称 ─────────────────────────────
            public const string Project_StaticImg_Text_Effect_GrayScale = nameof(Project_StaticImg_Text_Effect_GrayScale);
            public const string Project_StaticImg_Text_Effect_Invert = nameof(Project_StaticImg_Text_Effect_Invert);
            public const string Project_StaticImg_Text_Effect_Exposure = nameof(Project_StaticImg_Text_Effect_Exposure);
            public const string Project_StaticImg_Text_Effect_Brightness = nameof(Project_StaticImg_Text_Effect_Brightness);
            public const string Project_StaticImg_Text_Effect_Saturation = nameof(Project_StaticImg_Text_Effect_Saturation);
            public const string Project_StaticImg_Text_Effect_Hue = nameof(Project_StaticImg_Text_Effect_Hue);
            public const string Project_StaticImg_Text_Effect_Contrast = nameof(Project_StaticImg_Text_Effect_Contrast);
            public const string Project_StaticImg_Text_Effect_Temperature = nameof(Project_StaticImg_Text_Effect_Temperature);
            public const string Project_StaticImg_Text_Effect_Highlights = nameof(Project_StaticImg_Text_Effect_Highlights);
            public const string Project_StaticImg_Text_Effect_Sepia = nameof(Project_StaticImg_Text_Effect_Sepia);
            public const string Project_StaticImg_Text_Effect_Pixelate = nameof(Project_StaticImg_Text_Effect_Pixelate);
            public const string Project_StaticImg_Text_Effect_Emboss = nameof(Project_StaticImg_Text_Effect_Emboss);
            public const string Project_StaticImg_Text_Effect_Blur = nameof(Project_StaticImg_Text_Effect_Blur);
            public const string Project_StaticImg_Text_Effect_Sharpen = nameof(Project_StaticImg_Text_Effect_Sharpen);
            public const string Project_StaticImg_Text_Effect_Noise = nameof(Project_StaticImg_Text_Effect_Noise);
            public const string Project_StaticImg_Text_Effect_Vignette = nameof(Project_StaticImg_Text_Effect_Vignette);
            public const string Project_StaticImg_Text_Effect_Glow = nameof(Project_StaticImg_Text_Effect_Glow);
            public const string Project_StaticImg_Text_Effect_Bloom = nameof(Project_StaticImg_Text_Effect_Bloom);
            public const string Project_StaticImg_Text_Effect_Distort = nameof(Project_StaticImg_Text_Effect_Distort);
            public const string Project_StaticImg_Text_Effect_Multiply = nameof(Project_StaticImg_Text_Effect_Multiply);
            public const string Project_StaticImg_Text_Effect_Screen = nameof(Project_StaticImg_Text_Effect_Screen);
            public const string Project_StaticImg_Text_Effect_Overlay = nameof(Project_StaticImg_Text_Effect_Overlay);
            public const string Project_StaticImg_Text_Effect_SoftLight = nameof(Project_StaticImg_Text_Effect_SoftLight);
            // CanvasEffect 效果描述
            public const string Project_StaticImg_Text_EffectDesc_GrayScale = nameof(Project_StaticImg_Text_EffectDesc_GrayScale);
            public const string Project_StaticImg_Text_EffectDesc_Invert = nameof(Project_StaticImg_Text_EffectDesc_Invert);
            public const string Project_StaticImg_Text_EffectDesc_Exposure = nameof(Project_StaticImg_Text_EffectDesc_Exposure);
            public const string Project_StaticImg_Text_EffectDesc_Brightness = nameof(Project_StaticImg_Text_EffectDesc_Brightness);
            public const string Project_StaticImg_Text_EffectDesc_Saturation = nameof(Project_StaticImg_Text_EffectDesc_Saturation);
            public const string Project_StaticImg_Text_EffectDesc_Hue = nameof(Project_StaticImg_Text_EffectDesc_Hue);
            public const string Project_StaticImg_Text_EffectDesc_Contrast = nameof(Project_StaticImg_Text_EffectDesc_Contrast);
            public const string Project_StaticImg_Text_EffectDesc_Temperature = nameof(Project_StaticImg_Text_EffectDesc_Temperature);
            public const string Project_StaticImg_Text_EffectDesc_Highlights = nameof(Project_StaticImg_Text_EffectDesc_Highlights);
            public const string Project_StaticImg_Text_EffectDesc_Sepia = nameof(Project_StaticImg_Text_EffectDesc_Sepia);
            public const string Project_StaticImg_Text_EffectDesc_Pixelate = nameof(Project_StaticImg_Text_EffectDesc_Pixelate);
            public const string Project_StaticImg_Text_EffectDesc_Emboss = nameof(Project_StaticImg_Text_EffectDesc_Emboss);
            public const string Project_StaticImg_Text_EffectDesc_Blur = nameof(Project_StaticImg_Text_EffectDesc_Blur);
            public const string Project_StaticImg_Text_EffectDesc_Sharpen = nameof(Project_StaticImg_Text_EffectDesc_Sharpen);
            public const string Project_StaticImg_Text_EffectDesc_Noise = nameof(Project_StaticImg_Text_EffectDesc_Noise);
            public const string Project_StaticImg_Text_EffectDesc_Vignette = nameof(Project_StaticImg_Text_EffectDesc_Vignette);
            public const string Project_StaticImg_Text_EffectDesc_Glow = nameof(Project_StaticImg_Text_EffectDesc_Glow);
            public const string Project_StaticImg_Text_EffectDesc_Bloom = nameof(Project_StaticImg_Text_EffectDesc_Bloom);
            public const string Project_StaticImg_Text_EffectDesc_Distort = nameof(Project_StaticImg_Text_EffectDesc_Distort);
            public const string Project_StaticImg_Text_EffectDesc_Multiply = nameof(Project_StaticImg_Text_EffectDesc_Multiply);
            public const string Project_StaticImg_Text_EffectDesc_Screen = nameof(Project_StaticImg_Text_EffectDesc_Screen);
            public const string Project_StaticImg_Text_EffectDesc_Overlay = nameof(Project_StaticImg_Text_EffectDesc_Overlay);
            public const string Project_StaticImg_Text_EffectDesc_SoftLight = nameof(Project_StaticImg_Text_EffectDesc_SoftLight);
            public const string Settings_General_Version_FindNew = nameof(Settings_General_Version_FindNew);
            public const string AppUpdater_SpeedText_Ready = nameof(AppUpdater_SpeedText_Ready);
            public const string Settings_General_Version_DownloadStart = nameof(Settings_General_Version_DownloadStart);
            public const string Settings_General_Version_Install = nameof(Settings_General_Version_Install);
            public const string Settings_General_Version_InstallerReady = nameof(Settings_General_Version_InstallerReady);
            public const string Settings_General_Version_PluginsReady = nameof(Settings_General_Version_PluginsReady);
            public const string PluginUpdate_ReplacedPlugin = nameof(PluginUpdate_ReplacedPlugin);
            public const string PluginUpdate_Title_Completed = nameof(PluginUpdate_Title_Completed);
            public const string PluginUpdate_Title_Failed = nameof(PluginUpdate_Title_Failed);
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
