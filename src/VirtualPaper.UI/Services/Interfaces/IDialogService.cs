using System.Threading.Tasks;

namespace VirtualPaper.UI.Services.Interfaces {
    public interface IDialogService {
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

        Task<DialogResult> ShowDialogAsync(
            object content,
            string title,
            string primaryBtnText,
            bool isDefaultPrimary = true);

        Task<DialogResult> ShowDialogWithoutTitleAsync(
            object content,
            string primaryBtnText,
            bool isDefaultPrimary = true);

        public enum DialogResult {
            None,
            Primary,
            Seconday
        }
    }
}
