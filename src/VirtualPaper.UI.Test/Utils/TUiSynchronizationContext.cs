using VirtualPaper.Common.Utils.ThreadContext;

namespace VirtualPaper.UI.Test.Utils {
    public class TUiSynchronizationContext : IUiSynchronizationContext {
        public override void Post(Action action) => action(); // 直接同步执行

        public override Task PostAsync(Func<Task> asyncAction) => asyncAction();

        public override Task<T> PostAsync<T>(Func<Task<T>> asyncAction) => asyncAction();

        public override void Send(Action action) => action();
    }
}
