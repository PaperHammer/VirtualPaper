using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent {
    public static class WindowConsts {
        public static ArcWindow ArcWindowInstance { get; set; } = null!;
        public static nint WindowHandle { get; set; }
        //public static uint Dpi { get; set; }

        public static async Task<string?> GetStorageFolderAsync() {
            var storageFolder = await WindowsStoragePickers.PickFolderAsync(WindowHandle);
            if (storageFolder == null) return null;
            
            return storageFolder.Path;
        }
    }
}
