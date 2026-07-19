using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.DraftPanel.Services {
    public class WorkspaceSaveCoordinator : IWorkspaceSaveCoordinator {
        private readonly IGlobalDialogService _globalDialogService;

        public WorkspaceSaveCoordinator(IGlobalDialogService globalDialogService) {
            _globalDialogService = globalDialogService;
        }

        public async Task<bool> CanCloseAsync(IRuntime runtime, bool isSaved, bool canCancel) {
            if (isSaved) return true;

            var result = canCancel
                ? await ShowUnsavedDialogWithCancelAsync(runtime)
                : await ShowUnsavedDialogAsync(runtime);

            return result switch {
                DialogResult.Primary => await runtime.SaveAsync(),
                DialogResult.Secondary => true,
                _ => false,
            };
        }

        private Task<DialogResult> ShowUnsavedDialogAsync(IRuntime runtime) {
            return _globalDialogService.ShowDialogAsync(
                content: $"\"{runtime.FileName}\" {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Content))}",
                title: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Title))}",
                primaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Save))}",
                secondaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Unsave))}");
        }

        private Task<DialogResult> ShowUnsavedDialogWithCancelAsync(IRuntime runtime) {
            return _globalDialogService.ShowDialogAsync(
                content: $"\"{runtime.FileName}\" {LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Content))}",
                title: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Unsave_Intercept_Title))}",
                primaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Save))}",
                secondaryBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Unsave))}",
                closeBtnText: $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Text_Cancel))}");
        }
    }
}
