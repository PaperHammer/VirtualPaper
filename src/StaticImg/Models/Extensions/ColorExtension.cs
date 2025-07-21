using Windows.UI;

namespace Workloads.Creation.StaticImg.Models.Extensions {
    public static class ColorExtension {
        public static byte[] ToByteArgb(this Color color) {
            return [color.A, color.R, color.G, color.B];
        }
    }
}
