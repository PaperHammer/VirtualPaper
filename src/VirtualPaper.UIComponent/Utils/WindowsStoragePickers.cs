using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Windows.Storage.Pickers;
using Windows.Storage;

namespace VirtualPaper.UIComponent.Utils {
    /// <summary>
    /// Desktop pickers shared by WinUI pages. Windows App SDK pickers also work
    /// when the application is elevated, unlike Windows.Storage.Pickers.
    /// </summary>
    public static class WindowsStoragePickers {
        public static async Task<StorageFile[]> PickFilesAsync(nint hwnd, string[] fileTypeFilter, bool multiSelect = false) {
            var picker = new FileOpenPicker(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            foreach (var extension in fileTypeFilter) picker.FileTypeFilter.Add(extension);

            if (multiSelect) {
                var results = await picker.PickMultipleFilesAsync();
                var files = new List<StorageFile>(results.Count);
                foreach (var result in results) files.Add(await StorageFile.GetFileFromPathAsync(result.Path));
                return files.ToArray();
            }

            var file = await picker.PickSingleFileAsync();
            return file == null ? [] : [await StorageFile.GetFileFromPathAsync(file.Path)];
        }

        public static async Task<StorageFile?> PickSaveFileAsync(nint hwnd, string suggestedFileName,
            Dictionary<string, string[]> fileTypeChoices) {
            var picker = new FileSavePicker(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd)) {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = suggestedFileName
            };
            foreach (var choice in fileTypeChoices) picker.FileTypeChoices.Add(choice.Key, choice.Value);
            string? extension = fileTypeChoices.Values.SelectMany(extensions => extensions).FirstOrDefault();
            if (extension != null && extension != "*") picker.DefaultFileExtension = extension;

            var result = await picker.PickSaveFileAsync();
            if (result == null) return null;

            // The desktop picker returns a path without creating a file. Preserve
            // the existing StorageFile contract without truncating an existing file.
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(result.Path)!);
            return await folder.CreateFileAsync(Path.GetFileName(result.Path), CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder?> PickFolderAsync(nint hwnd) {
            var picker = new FolderPicker(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            var result = await picker.PickSingleFolderAsync();
            return result == null ? null : await StorageFolder.GetFolderFromPathAsync(result.Path);
        }
    }
}
