using System;

namespace VirtualPaper.UIComponent.Attributes {
    /// <summary>
    /// 页面被导航移除后仍保持在视觉树中
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class KeepAliveAttribute : Attribute {
        public bool Value { get; } = true;

        public KeepAliveAttribute(bool value = true) => Value = value;
    }
}
