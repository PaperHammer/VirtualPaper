using System;

namespace VirtualPaper.UIComponent.Attributes {
    /// <summary>
    /// 页面是否在导航后仍保持在内存中继续运行
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class KeepAliveAttribute : Attribute {
        public bool Value { get; } = true;

        public KeepAliveAttribute(bool value = true) => Value = value;
    }
}
