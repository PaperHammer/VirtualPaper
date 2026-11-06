using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Storage.Adapter;
using Windows.Storage;

namespace VirtualPaper.UIComponent.Utils.Adapter {
    public class StoragePickerWrapper : IStoragePicker {
        public async Task<IStorageItem[]?> PickFilesAsync(nint hwnd, string[] extensions, bool multiSelect) =>
            await WindowsStoragePickers.PickFilesAsync(hwnd, extensions, multiSelect);

        public async Task<IStorageFolder?> PickFolderAsync(nint hwnd) =>
            await WindowsStoragePickers.PickFolderAsync(hwnd);

        public async Task<IStorageFile?> PickSaveFileAsync(nint hwnd, string suggestedFileName, Dictionary<string, string[]> fileTypeChoices) =>
            await WindowsStoragePickers.PickSaveFileAsync(hwnd, suggestedFileName, fileTypeChoices);
    }
}
