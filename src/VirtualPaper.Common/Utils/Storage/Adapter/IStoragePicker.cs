using Windows.Storage;

namespace VirtualPaper.Common.Utils.Storage.Adapter {
    public interface IStoragePicker {
        Task<IStorageItem[]?> PickFilesAsync(IntPtr hwnd, string[] extensions, bool multiSelect);
        Task<IStorageFolder?> PickFolderAsync(IntPtr hwnd);
        Task<IStorageFile?> PickSaveFileAsync(nint hwnd, string suggestedFileName, Dictionary<string, string[]> fileTypeChoices);
    }
}
