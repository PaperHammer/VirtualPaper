using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace VirtualPaper.UIComponent.Utils;

/// <summary>组件显式提供自己的菜单；共享菜单在最后一个使用者释放后恢复原值。</summary>
internal static class UiFlyoutAnimation {
    private static readonly ConditionalWeakTable<FlyoutBase, State> States = new();

    internal static IDisposable Suppress(FlyoutBase flyout) {
        var state = States.GetValue(flyout, static target => new State(target.AreOpenCloseAnimationsEnabled));
        if (state.Users++ == 0) flyout.Opening += Opening;
        flyout.AreOpenCloseAnimationsEnabled = false;
        return new Lease(flyout, state);
    }

    private static void Opening(object? sender, object args) {
        if (sender is FlyoutBase flyout) flyout.AreOpenCloseAnimationsEnabled = false;
    }

    private sealed class State(bool original) {
        internal bool Original { get; } = original;
        internal int Users;
    }

    private sealed class Lease(FlyoutBase flyout, State state) : IDisposable {
        private FlyoutBase? _flyout = flyout;
        public void Dispose() {
            var target = _flyout;
            if (target == null) return;
            _flyout = null;
            if (--state.Users > 0) return;
            target.Opening -= Opening;
            target.AreOpenCloseAnimationsEnabled = state.Original;
            States.Remove(target);
        }
    }
}
