using VirtualPaper.UIComponent.Context;

namespace VirtualPaper.PlayerWeb.Core.Models {
    public record GeneralEffectStartArgs(
        ArcPageContext PageContext,
        string EffectFilePathUsing, string EffectFilePathTemporary, string EffectFilePathTemplate) { }
}
