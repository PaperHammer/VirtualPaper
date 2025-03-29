using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IDraftPanelBridge : IPanelBridge, ILogBridge {
        /// <summary>
        /// 父级共享参数
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// 根据当前页面，参数的类型应如下：
        /// <list type="bullet">
        ///     <item>
        ///         <term><see cref="DraftPanelState.ConfigSpace"/></term>
        ///         <description>参数类型为 <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.WorkSpace"/></term>
        ///         <description>参数类型为 <see cref="Model.NavParam.ToWorkSpace"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.GetStart"/></term>
        ///         <description>参数类型为 <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.ProjectConfig"/></term>
        ///         <description>参数类型为 <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.DraftConfig"/></term>
        ///         <description>参数类型为 <see cref="Model.NavParam.ToDraftConfig"/></description>
        ///     </item>
        /// </list>
        /// </remarks>
        object GetSharedData();
        /// <summary>
        /// 导航到目标页面
        /// </summary>
        /// <param name="nextPanel"></param>
        /// <param name="sharedData"></param>
        /// <returns></returns>
        /// <remarks>
        /// 根据目标页面，参数的类型应如下：
        /// <list type="bullet">
        ///     <item>
        ///         <term><see cref="DraftPanelState.ConfigSpace"/></term>
        ///         <description>参数类型为 <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.WorkSpace"/></term>
        ///         <description>参数类型为 <see cref="Model.NavParam.ToWorkSpace"/> | <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.GetStart"/></term>
        ///         <description>参数类型为 <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.ProjectConfig"/></term>
        ///         <description>参数类型为 <see cref="Nullable"/></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="DraftPanelState.DraftConfig"/></term>
        ///         <description>参数类型为 <see cref="Model.NavParam.ToDraftConfig"/> | <see cref="Nullable"/></description>
        ///     </item>
        /// </list>
        /// </remarks>
        void ChangePanelState(DraftPanelState nextPanel, object data);
        uint GetDpi();
    }
}
