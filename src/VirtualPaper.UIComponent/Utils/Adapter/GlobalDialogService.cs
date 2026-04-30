using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;

namespace VirtualPaper.UIComponent.Utils.Adapter {
    public class GlobalDialogService : IGlobalDialogService {
        public ContentDialog? CreateDialog(object content, string title, string primaryBtnText, string secondaryBtnText, bool isDefaultPrimary = true) {
            return GlobalDialogUtils.CreateDialog(content, title, primaryBtnText, secondaryBtnText, isDefaultPrimary);
        }

        public ContentDialog? CreateDialog(object content, string title, string primaryBtnText, bool isDefaultPrimary = true) {
            return GlobalDialogUtils.CreateDialog(content, title, primaryBtnText, isDefaultPrimary);
        }

        public ContentDialog? CreateDialogWithoutTitle(object content, string primaryBtnText, string secondaryBtnText, bool isDefaultPrimary = true) {
            return GlobalDialogUtils.CreateDialogWithoutTitle(content, primaryBtnText, secondaryBtnText, isDefaultPrimary);
        }

        public ContentDialog? CreateDialogWithoutTitle(object content, string primaryBtnText, bool isDefaultPrimary = true) {
            return GlobalDialogUtils.CreateDialogWithoutTitle(content, primaryBtnText, isDefaultPrimary);
        }

        public async Task ShowDialogAsync(string message, string title, string primaryBtnText) {
            await GlobalDialogUtils.ShowDialogAsync(message, title, primaryBtnText);
        }

        public async Task<DialogResult> ShowDialogAsync(object content, string title, string primaryBtnText, string secondaryBtnText, bool isDefaultPrimary = true) {
            return await GlobalDialogUtils.ShowDialogAsync(content, title, primaryBtnText, secondaryBtnText, isDefaultPrimary);
        }

        public async Task<DialogResult> ShowDialogAsync(object content, string title, string primaryBtnText, bool isDefaultPrimary = true) {
            return await GlobalDialogUtils.ShowDialogAsync(content, title, primaryBtnText, isDefaultPrimary);
        }

        public async Task<DialogResult> ShowDialogAsync(object content, string title, string primaryBtnText, string secondaryBtnText, string closeBtnText, bool isDefaultPrimary = true) {
            return await GlobalDialogUtils.ShowDialogAsync(content, title, primaryBtnText, secondaryBtnText, closeBtnText, isDefaultPrimary);
        }

        public async Task<DialogResult> ShowDialogWithoutTitleAsync(object content, string primaryBtnText, string secondaryBtnText, bool isDefaultPrimary = true) {
            return await GlobalDialogUtils.ShowDialogWithoutTitleAsync(content, primaryBtnText, secondaryBtnText, isDefaultPrimary);
        }

        public async Task<DialogResult> ShowDialogWithoutTitleAsync(object content, string primaryBtnText, bool isDefaultPrimary = true) {
            return await GlobalDialogUtils.ShowDialogWithoutTitleAsync(content, primaryBtnText, isDefaultPrimary);
        }
    }
}
