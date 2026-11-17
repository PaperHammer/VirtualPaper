using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VirtualPaper.UIComponent.Templates;

namespace VirtualPaper.UIComponent.Utils;

// The scope follows this async operation, rather than suppressing messages from
// unrelated operations or retaining a process-wide history of message keys.
internal sealed class GlobalMessageBatch : IDisposable {
    internal readonly record struct Entry(ArcWindow? Window, GlobalMsgInfo Message);
    private static readonly AsyncLocal<GlobalMessageBatch?> Current = new();
    private readonly GlobalMessageBatch? _parent;
    private readonly Action<IReadOnlyList<Entry>> _publish;
    private readonly object _gate = new();
    private readonly List<Entry> _messages = [];
    private bool _disposed;

    private GlobalMessageBatch(Action<IReadOnlyList<Entry>> publish) {
        _parent = Current.Value;
        _publish = publish;
        Current.Value = this;
    }

    internal static IDisposable Begin(Action<IReadOnlyList<Entry>> publish) => new GlobalMessageBatch(publish);

    internal static bool TryAdd(Entry message) {
        for (var batch = Current.Value; batch != null; batch = batch._parent) {
            lock (batch._gate) {
                if (batch._disposed) continue;
                batch._messages.Add(message);
                return true;
            }
        }
        return false;
    }

    internal static void Remove(ArcWindow? window, string key) {
        for (var batch = Current.Value; batch != null; batch = batch._parent) {
            lock (batch._gate) {
                batch._messages.RemoveAll(entry => ReferenceEquals(entry.Window, window) && entry.Message.Key == key);
            }
        }
    }

    internal static IEnumerable<(ArcWindow? Window, GlobalMsgInfo[] Messages)> Group(IReadOnlyList<Entry> entries) =>
        entries.GroupBy(entry => (entry.Window, entry.Message.Severity))
            .Select(group => (group.Key.Window, group.Select(entry => entry.Message)
                .DistinctBy(message => (message.Message, message.TechnicalDetails)).ToArray()));

    public void Dispose() {
        Entry[] messages;
        lock (_gate) {
            if (_disposed) return;
            _disposed = true;
            messages = _messages.ToArray();
            _messages.Clear();
        }
        if (ReferenceEquals(Current.Value, this)) Current.Value = _parent;
        // Nested flows pass their original messages to the outer operation.
        // Late notifications from completed scopes fall through to normal delivery.
        var remaining = messages.Where(message => !TryAdd(message)).ToArray();
        if (remaining.Length > 0) _publish(remaining);
    }
}
