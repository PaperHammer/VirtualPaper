using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Creation.WebBackdrop {
    public sealed partial class MainPage : ArcPage, IRuntime {
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        public string FileName => Session.DesignFileUtil.ProjectName;
        public string FileNameWithoutEx => Session.DesignFileUtil.ProjectName;
        public string Id => Session.SessionId;
        public override Type ArcType => typeof(MainPage);
        protected override bool IsMultiInstance => true;
        public WebProjectSession Session { get; private set; } = null!;
        public bool IsSavedFromInit => Session.DesignFileUtil.IsSaveFromInit;

        public MainPage() {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            try {
                var identify = Payload?.Get<string>(NaviPayloadKey.StaticImgFileName) ?? string.Empty;
                Session = new WebProjectSession(identify);
                Session.IsSavedChanged += Session_IsSavedChanged;

                var payload = new FrameworkPayload() {
                    [NaviPayloadKey.ArcPageContext] = this.ArcContext,
                    [NaviPayloadKey.WebProjectSession] = this.Session
                };
                webEditor.Payload = payload;

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
                var result = await webEditor.ViewModel.SaveAllAsync();
                if (result) {
                    await webEditor.ViewModel.UpdateRecentUsedAsync(Session.DesignFileUtil.ProjectFolder);
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
            // Web editor doesn't have undo/redo yet
            return Task.CompletedTask;
        }

        public Task RedoAsync() {
            // Web editor doesn't have undo/redo yet
            return Task.CompletedTask;
        }

        public Task<string?> ExportAsync(ExportImageFormat format) {
            // Web project doesn't export as image
            return Task.FromResult<string?>(null);
        }
        #endregion
    }
}
