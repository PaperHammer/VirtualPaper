using Windows.ApplicationModel.DataTransfer;

namespace VirtualPaper.UI.Utils {
    internal class ClipboardUtil {
        public static void Copy(string text) {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
    }
}
