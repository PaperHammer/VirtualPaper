using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Grpc.Client.Interfaces;

namespace VirtualPaper.UI.Services;

// Delivery/acknowledgement stays separate from navigation and project loading.
// All entry points run on the UI thread; the gate serializes asynchronous opens.
internal sealed class ProjectOpenCoordinator(
    ICommandsClient commands,
    Func<IReadOnlyList<string>, Task> openProjects,
    Action<Exception> reportError) {
    public Task StartAsync() {
        if (_ready) return Task.CompletedTask;
        _ready = true;
        return ProcessAsync();
    }

    public async Task ProcessAsync() {
        if (!_ready || _paused || _stopped) return;
        await _gate.WaitAsync();
        try {
            while (!_paused && !_stopped) {
                var request = await commands.GetPendingProjectOpenAsync();
                if (request == null || _paused || _stopped) break;
                try { await openProjects(request.Paths); }
                catch (OperationCanceledException) { break; } // Leave interrupted work available for replay.
                catch (Exception ex) { reportError(ex); } // A bad project must not loop on every launch.
                await commands.CompleteProjectOpenAsync(request.Id);
            }
        }
        catch (Exception ex) { reportError(ex); }
        finally { _gate.Release(); }
    }

    public async Task PauseAsync() {
        _paused = true;
        await _gate.WaitAsync();
        _gate.Release();
    }

    public Task ResumeAsync() {
        _paused = false;
        return ProcessAsync();
    }

    public void Stop() => _stopped = true;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready, _paused, _stopped;
}
