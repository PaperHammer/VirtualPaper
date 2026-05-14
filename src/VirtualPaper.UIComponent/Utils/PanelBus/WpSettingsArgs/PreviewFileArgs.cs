using VirtualPaper.UIComponent.Context;

namespace VirtualPaper.UIComponent.Utils.PanelBus.WpSettingsArgs {
    public record PreviewFileArgs(string FilePath, ArcPageContext? Ctx);
}
