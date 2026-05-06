using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;

namespace VirtualPaper.UIComponent.Utils.Adapter.Interfaces {
    public interface IGlobalDialogService {
        Task ShowDialogAsync(
            string message,
            string title,
            string primaryBtnText);

        Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true);

        ContentDialog? CreateDialog(
            object content,
            string title,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true);

        Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            bool isDefaultPrimary = true);

        Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            string secondaryBtnText,
            string closeBtnText,
            bool isDefaultPrimary = true);

        ContentDialog? CreateDialog(
            object content,
            string title,
            string primaryBtnText,
            bool isDefaultPrimary = true);

        Task<DialogResult> ShowDialogWithoutTitleAsync(
            object content,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true);

        ContentDialog? CreateDialogWithoutTitle(
            object content,
            string primaryBtnText,
            string secondaryBtnText,
            bool isDefaultPrimary = true);

        Task<DialogResult> ShowDialogWithoutTitleAsync(
            object content,
            string primaryBtnText,
            bool isDefaultPrimary = true);

        ContentDialog? CreateDialogWithoutTitle(
            object content,
            string primaryBtnText,
            bool isDefaultPrimary = true);
    }
}
