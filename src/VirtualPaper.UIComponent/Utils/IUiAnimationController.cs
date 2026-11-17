namespace VirtualPaper.UIComponent.Utils;

/// <summary>由组件控制自己的动画，关闭时直接呈现最终状态，不改变功能或编辑内容。</summary>
public interface IUiAnimationController {
    void SetAnimationsEnabled(bool enabled);
}
