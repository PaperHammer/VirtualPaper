using System.Text.Json;
using System.Text.Json.Nodes;

namespace VirtualPaper.Common.Runtime.PlayerWeb {
    public static class ParallaxProperties {
        // Called only for depth-image wallpapers; defaults preserve the previous playback.
        public static void EnsureDepthProperties(JsonObject properties) {
            if (!properties.ContainsKey(nameof(Picture3DCostumize.ParallaxStrength))) {
                properties[nameof(Picture3DCostumize.ParallaxStrength)] = JsonSerializer.SerializeToNode(
                    new ParallaxStrength(), Picture3DCostumizeContext.Default.ParallaxStrength);
            }
            if (!properties.ContainsKey(nameof(Picture3DCostumize.ParallaxAutoDrift))) {
                properties[nameof(Picture3DCostumize.ParallaxAutoDrift)] = JsonSerializer.SerializeToNode(
                    new ParallaxAutoDrift(), Picture3DCostumizeContext.Default.ParallaxAutoDrift);
            }
        }
    }
}
