using System.Threading.Tasks;

namespace VirtualPaper.UIComponent.Navigation.Interfaces {
    public interface IConfirmClose {
        /// <summary>
        /// 检查页面是否允许被关闭（如果未保存，弹出提示对话框）
        /// 返回 true 表示允许关闭；返回 false 表示用户取消了关闭。
        /// </summary>
        Task<bool> CanCloseAsync();
    }
}
