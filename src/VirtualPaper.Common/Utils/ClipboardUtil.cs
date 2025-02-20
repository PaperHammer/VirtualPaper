using Windows.ApplicationModel.DataTransfer;

namespace VirtualPaper.Common.Utils {
    public class ClipboardUtil {
        public static void Copy(string text) {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
    }
}
