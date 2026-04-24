using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent;
using Windows.Storage;
using UAC = UACHelper.UACHelper;

namespace VirtualPaper.WpSettingsPanel.ViewModels {
    public class AddToLibViewModel {
        public event EventHandler<IReadOnlyList<IStorageItem>>? OnRequestAddFile;
        public event EventHandler<StorageFolder>? OnRequestAddFolder;

        public ICommand? HandleAddFilesCommand;
        public ICommand? HandleAddFoldersCommand;

        public bool IsElevated { get; }

        public AddToLibViewModel() {
            IsElevated = UAC.IsElevated;

            InitCommand();
        }

        private void InitCommand() {
            HandleAddFilesCommand = new RelayCommand(async () => {
                await FileBrowseActionAsync();
            });
            HandleAddFoldersCommand = new RelayCommand(async () => {
                await FolderBrowseActionAsync();
            });
        }

        private async Task FileBrowseActionAsync() {
            var storage = await WindowsStoragePickers.PickFilesAsync(
                WindowConsts.WindowHandle,
                [.. FileFilter.FileTypeToExtension[FileType.FImage], .. FileFilter.FileTypeToExtension[FileType.FGif], .. FileFilter.FileTypeToExtension[FileType.FVideo]],
                true);
            if (storage == null || storage.Length < 1) return;

            AddWallpaperFiles(storage);
        }
                
        private async Task FolderBrowseActionAsync() {
            var storage = await WindowsStoragePickers.PickFolderAsync(WindowConsts.WindowHandle);
            if (storage == null) return;

            AddWallpaperFolder(storage);
        }

        internal void AddWallpaperFiles(IReadOnlyList<IStorageItem> filePaths) => OnRequestAddFile?.Invoke(this, filePaths);
        internal void AddWallpaperFolder(StorageFolder storageFolder) => OnRequestAddFolder?.Invoke(this, storageFolder);
    }
}
