using Windows.Storage;

namespace VirtualPaper.Common.Utils.Storage.Adapter {
    public class StoragePickerWrapper : IStoragePicker {
        public async Task<IStorageItem[]?> PickFilesAsync(
           IntPtr hwnd,
           string[] extensions,
           bool multiSelect) {
            return await WindowsStoragePickers.PickFilesAsync(hwnd, extensions, multiSelect);
        }

        public async Task<IStorageFolder?> PickFolderAsync(IntPtr hwnd) {
            return await WindowsStoragePickers.PickFolderAsync(hwnd);
        }

        public async Task<IStorageFile?> PickSaveFileAsync(nint hwnd, string suggestedFileName, Dictionary<string, string[]> fileTypeChoices) {
            return await WindowsStoragePickers.PickSaveFileAsync(hwnd, suggestedFileName, fileTypeChoices);
        }
    }
}
