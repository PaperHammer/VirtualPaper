using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.UndoRedo.Events;
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
    public sealed partial class MainPage : ArcPage, IRuntime {
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        public string FileName => Session.DesignFileUtil.ProjectFilePath;
        public string FileNameWithoutEx => Session.DesignFileUtil.ProjectName;
        public string Id => Session.SessionId;
        public override Type ArcType => typeof(MainPage);
        protected override bool IsMultiInstance => true;
        public WebProjectSession Session { get; private set; } = null!;
        public bool IsSavedFromInit => Session.DesignFileUtil.IsSaveFromInit;

        public MainPage() {
            this.InitializeComponent();
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        /// <summary>
        /// 由 RuntimeFactory 在构造后调用，传入文件路径完成初始化
        /// </summary>
        /// <param name="identify">类型为 web 项目（zip/rar/7z）的文件路径或项目名称</param>
        public void Initialize(string identify, FileType fileType) {
            Session = new WebProjectSession(identify);
            Payload = new FrameworkPayload() {
                [NaviPayloadKey.ArcPageContext] = this.ArcContext,
                [NaviPayloadKey.WebProjectSession] = this.Session
            };
            Session.IsSavedChanged += Session_IsSavedChanged;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            try {
                webEditor.Payload = Payload;
                IsEnabled = true;
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
                GlobalMessageUtil.ShowException(ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) {
            Session.IsSavedChanged -= Session_IsSavedChanged;
            Session.Dispose();
        }

        private void Session_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            IsSavedChanged?.Invoke(this, e);
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

        public Task<string?> ExportAsync(ExportImageFormat format) {
            // Web project doesn't export as image
            return Task.FromResult<string?>(null);
        }
        #endregion
    }
}
