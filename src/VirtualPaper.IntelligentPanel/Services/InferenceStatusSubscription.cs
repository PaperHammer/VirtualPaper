using System;
using VirtualPaper.ML.Inference;

namespace VirtualPaper.IntelligentPanel.Services;

/// <summary>仅跟踪当前任务阶段；结束后保留最终状态，忽略尚未派发的旧通知。</summary>
internal sealed class InferenceStatusSubscription : IDisposable {
    private readonly IInferenceStatusSource _source;
    private readonly Action<InferenceState> _update;
    private readonly Action<Action> _dispatch;
    private readonly object _gate = new();
    private InferenceState _latest;
    private bool _disposed;

    internal InferenceStatusSubscription(IInferenceStatusSource source, Action<InferenceState> update, Action<Action> dispatch) {
        _source = source;
        _update = update;
        _dispatch = dispatch;
        _latest = source.InferenceState;
        source.InferenceStateChanged += StateChanged;
        StateChanged(source, source.InferenceState);
    }

    private void StateChanged(object? sender, InferenceState state) {
        lock (_gate) {
            if (_disposed) return;
            // 初始化读取或迟到通知可能携带旧值，以模型当前快照为准。
            _latest = _source.InferenceState;
        }
        _dispatch(() => {
            InferenceState latest;
            lock (_gate) {
                if (_disposed) return;
                latest = _latest;
            }
            _update(latest);
        });
    }

    // 由任务所在的 UI 线程释放，同时补上最后一次状态，不依赖通知派发的速度。
    public void Dispose() {
        InferenceState latest;
        lock (_gate) {
            if (_disposed) return;
            _disposed = true;
            latest = _latest;
        }
        _source.InferenceStateChanged -= StateChanged;
        _update(latest);
    }
}
