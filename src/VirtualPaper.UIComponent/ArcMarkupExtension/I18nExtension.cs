using Microsoft.UI.Xaml.Markup;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.ArcMarkupExtension {
    /// <summary>
    /// 推荐直接在 xaml 中使用 x:Bind 模式绑定文本内容
    /// </summary>
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public partial class I18nExtension : MarkupExtension {
        public string Key { get; set; } = null!;

        protected override object ProvideValue() {
            return LanguageUtil.GetI18n(Key);
        }
    }
}
