using VirtualPaper.Common.Utils.ThreadContext;

namespace VirtualPaper.UI.Test.Utils {
    public class T_UiSynchronizationContext : IUiSynchronizationContext {
        public SynchronizationContext? Current => SynchronizationContext.Current;

        public override void Post(Action action) => action(); // 直接同步执行
        public override void Send(Action action) => action();
    }
}
