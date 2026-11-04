using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using VirtualPaper.UIComponent;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Workloads.Creation.WebBackdrop {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage, IRuntime, IRuntimeTopBarContentProvider, IRuntimeEditCommandProvider {
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        public string FileName => Session.DesignFileUtil.ProjectFilePath;
        public string FileNameWithoutEx => Session.DesignFileUtil.ProjectName;
        public string ProjectFilePath => Session.DesignFileUtil.ProjectFilePath;
        public string Id => Session.SessionId;
        public override Type ArcType => typeof(MainPage);
        protected override bool IsMultiInstance => true;
        public WebProjectSession Session { get; private set; } = null!;
        public bool IsSavedFromInit => Session.DesignFileUtil.IsSaveFromInit;

        public FileType RuntimeFileType { get; private set; }
        public FrameworkElement TopBarContent => webEditor.TopBarContent;
        public void SetTopBarContentActive(bool isActive) => webEditor.SetQuickOpenActive(isActive);
        public event EventHandler? EditCommandStateChanged {
            add => webEditor.EditCommandStateChanged += value;
            remove => webEditor.EditCommandStateChanged -= value;
        }
        public bool CanExecuteEditCommand(RuntimeEditCommand command) => webEditor.CanExecuteEditCommand(command);
        public Task ExecuteEditCommandAsync(RuntimeEditCommand command) => webEditor.ExecuteEditCommandAsync(command);

        public MainPage() {
            this.InitializeComponent();
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        /// <summary>
        /// 由 RuntimeFactory 在构造后调用，传入文件路径完成初始化
        /// </summary>
        /// <param name="identify">类型为 web 项目（zip/rar/7z）的文件路径或项目名称</param>
        public void Initialize(string identify, FileType fileType) {
            RuntimeFileType = fileType;
            Session = new WebProjectSession(identify);
            Payload = new FrameworkPayload() {
                [NaviPayloadKey.WebProjectSession] = this.Session,
                [NaviPayloadKey.ContextKey] = this.ContextKey,
            };
            Session.IsSavedChanged += Session_IsSavedChanged;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            IsEnabled = false;
            try {
                var loadingCtx = ArcContext.LoadingContext;
                if (loadingCtx == null) {
                    webEditor.Payload = Payload;
                    await webEditor.InitializeAsync();
                    return;
                }

                await loadingCtx.RunAsync(async _ => {
                    // Give WinUI a chance to render this page's loading overlay
                    // before the editor builds its project UI.
                    await Task.Yield();
                    webEditor.Payload = Payload;
                    await webEditor.InitializeAsync();
                });
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            finally {
                IsEnabled = true;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            Session.IsSavedChanged -= Session_IsSavedChanged;
            Session.Dispose();
        }

        private void Session_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            IsSavedChanged?.Invoke(this, e);
        }

        private async void WebEditor_SaveRequested(object? sender, EventArgs e) {
            try {
                var result = await webEditor.SaveActiveFileAsync();
                if (result) {
                    await webEditor.ViewModel.UpdateRecentUsedAsync(Session.DesignFileUtil.ProjectFilePath);
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private async void WebEditor_SaveAllRequested(object? sender, EventArgs e) {
            await SaveAsync();
        }

        #region IRuntime
        public async Task<bool> SaveAsync() {
            try {
                var result = await webEditor.SaveAllAsync();
                if (result) {
                    await webEditor.ViewModel.UpdateRecentUsedAsync(Session.DesignFileUtil.ProjectFilePath);
                }
                return result;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            return false;
        }

        public async Task<string?> SaveAsAsync() {
            try {
                // TODO: Implement SaveAs for web project
                throw new NotImplementedException();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            return null;
        }

        public Task UndoAsync() {
            return webEditor.UndoAsync();
        }

        public Task RedoAsync() {
            return webEditor.RedoAsync();
        }

        public Task<bool> AddToLibraryAsync() {
            return webEditor.AddToLibraryAsync();
        }

        public async Task<string?> ExportAsync(ExportImageFormat format) {
            // Web 项目导出两种 zip（选择保存位置 → 打包 → 写入，与 StaticImg 导出流程一致）：
            //  - Zip    ：库可导入的 FWebZip 标准 Web 壁纸包（剔除 .vpw，包内含 project.json）
            //  - FullZip：全量项目归档（含 .vpw 工程文件，用于备份/迁移）
            try {
                var isFullExport = format == ExportImageFormat.FullZip;
                Dictionary<string, string[]> fileTypeChoices = isFullExport
                    ? new() { ["Full Project Archive (*.zip)"] = [".zip"] }
                    : new() { ["Web Wallpaper Package (*.zip)"] = [".zip"] };

                var saveFile = await WindowsStoragePickers.PickSaveFileAsync(
                    WindowConsts.WindowHandle,
                    string.Concat(Session.DesignFileUtil.ProjectName, isFullExport ? "_full" : string.Empty, WebProjectExporter.ExportExtension),
                    fileTypeChoices
                );

                if (saveFile == null || string.IsNullOrEmpty(saveFile.Path))
                    return null;

                // 导出前先保存所有未保存的编辑，保证包内文件为最新内容
                if (!await webEditor.SaveAllAsync()) {
                    GlobalMessageUtil.ShowError(LanguageUtil.GetI18n("WebBackdrop_ExportSaveFailed"));
                    return null;
                }

                return isFullExport
                    ? await Task.Run(() => WebProjectExporter.ExportFull(Session.DesignFileUtil, saveFile.Path))
                    : await Task.Run(() => WebProjectExporter.Export(Session.DesignFileUtil, saveFile.Path));
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
            return null;
        }
        #endregion
    }
}
