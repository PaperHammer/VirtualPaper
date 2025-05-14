using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;

namespace VirtualPaper.Common.Utils.Storage {
    public static partial class WindowsStoragePickers {
        [GeneratedComInterface]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public partial interface IInitializeWithWindow {
            void Initialize(IntPtr hwnd);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        private interface IWindowNative {
            IntPtr WindowHandle { get; }
        }

        /// <summary>
        /// Sets the owner window for this picker. This is required when running in WinUI for Desktop.
        /// </summary>
        private static void SetOwnerWindow(nint hwnd, IInitializeWithWindow picker) {
            // ref: https://learn.microsoft.com/zh-cn/answers/questions/1518337/winui3-folderpicker
            // Associate the HWND with the file picker  
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        /// <summary>
        /// Opens a FileOpenPicker and returns the selected file(s).
        /// </summary>
        /// <param name="window">The owner window.</param>
        /// <param name="fileTypeFilter">An array of file types to filter by.</param>
        /// <param name="multiSelect">Whether to allow multiple files selection.</param>
        /// <returns>An array of StorageFile objects representing the selected files.</returns>
        public static async Task<StorageFile[]> PickFilesAsync(nint hwnd, string[] fileTypeFilter, bool multiSelect = false) {
            var picker = new FileOpenPicker();
            foreach (var fileType in fileTypeFilter) {
                picker.FileTypeFilter.Add(fileType);
            }

            SetOwnerWindow(hwnd, picker.As<IInitializeWithWindow>());

            if (multiSelect) {
                var files = await picker.PickMultipleFilesAsync();
                return files?.ToArray() ?? [];
            }
            else {
                var file = await picker.PickSingleFileAsync();
                return file != null ? [file] : [];
            }
        }

        /// <summary>
        /// Opens a FileSavePicker and returns the selected file.
        /// </summary>
        /// <param name="window">The owner window.</param>
        /// <param name="suggestedFileName">The default name for the file to save.</param>
        /// <param name="fileTypeFilter">An array of file types to filter by.</param>
        /// <returns>A StorageFile object representing the selected file.</returns>
        public static async Task<StorageFile> PickSaveFileAsync(nint hwnd, string suggestedFileName, Dictionary<string, string[]> fileTypeChoices) {
            var picker = new FileSavePicker {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = suggestedFileName
            };

            foreach (var choice in fileTypeChoices) {
                picker.FileTypeChoices.Add(choice.Key, choice.Value);
            }

            SetOwnerWindow(hwnd, picker.As<IInitializeWithWindow>());

            return await picker.PickSaveFileAsync();
        }

        /// <summary>
        /// Opens a FolderPicker and returns the selected folder.
        /// </summary>
        /// <param name="window">The owner window.</param>
        /// <returns>A StorageFolder object representing the selected folder.</returns>
        public static async Task<StorageFolder> PickFolderAsync(nint hwnd) {
            var picker = new FolderPicker {
                SuggestedStartLocation = PickerLocationId.HomeGroup,
            };

            SetOwnerWindow(hwnd, picker.As<IInitializeWithWindow>());

            return await picker.PickSingleFolderAsync();
        }
    }
}
