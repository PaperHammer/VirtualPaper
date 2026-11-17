using System;
using System.Threading.Tasks;
using VirtualPaper.AppSettingsPanel.Views;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.AppSettingsPanel.Utils;

public sealed class AiUseNoticeService : IAiUseNoticeService {
    // Bump only for substantive changes to AI processing or rights, not wording or translations.
    internal const int CurrentVersion = 1;

    public AiUseNoticeService(IUserSettingsClient settings) : this(settings, AiUseNoticeDialog.ShowAsync) { }

    internal AiUseNoticeService(IUserSettingsClient settings, Func<Task<bool>> showNotice) {
        _settings = settings;
        _showNotice = showNotice;
    }

    public bool IsAcknowledged => _pending == null && _settings.Settings.AiUseNoticeVersion >= CurrentVersion;

    public Task<bool> EnsureAcknowledgedAsync() {
        if (IsAcknowledged) return Task.FromResult(true);
        if (_pending != null) return _pending;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion.Task;
        _ = CompleteAsync(completion);
        return completion.Task;
    }

    private async Task CompleteAsync(TaskCompletionSource<bool> completion) {
        try {
            bool accepted = await _showNotice();
            if (accepted) {
                int previous = _settings.Settings.AiUseNoticeVersion;
                _settings.Settings.AiUseNoticeVersion = CurrentVersion;
                try { await _settings.SaveAsync<ISettings>(); }
                catch {
                    _settings.Settings.AiUseNoticeVersion = previous;
                    throw;
                }
            }
            _pending = null;
            completion.SetResult(accepted);
        }
        catch (Exception ex) {
            _pending = null;
            completion.SetException(ex);
        }
    }

    private readonly IUserSettingsClient _settings;
    private readonly Func<Task<bool>> _showNotice;
    private Task<bool>? _pending;
}
