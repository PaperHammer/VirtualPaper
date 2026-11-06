using System;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common.Logging;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.IntelligentPanel.Services {
    // Owned by the UI process, independently of the currently displayed page.
    public sealed class IntelligentTaskCloseCoordinator {
        private readonly StyleTranferViewModel _style;
        private readonly SuperResolutionViewModel _superResolution;
        private readonly IUserSettingsClient _settings;
        private readonly IIntelligentTaskDialogs _dialogs;
        private bool _dontRemind;

        public IntelligentTaskCloseCoordinator(StyleTranferViewModel style, SuperResolutionViewModel superResolution,
            IUserSettingsClient settings, IIntelligentTaskDialogs dialogs) {
            _style = style;
            _superResolution = superResolution;
            _settings = settings;
            _dialogs = dialogs;
        }

        private IntelligentTaskItem[] Tasks => _style.Tasks.Cast<IntelligentTaskItem>().Concat(_superResolution.Tasks).ToArray();

        public async Task<bool> CanCloseAsync() {
            _dontRemind = false;
            var tasks = Tasks;
            int active = tasks.Count(t => t.IsPending || t.IsProcessing || t.IsPreserving);
            int temporary = tasks.Count(t => t.IsTemporaryResult);
            bool remind = _settings.Settings.RemindUnpreservedIntelligentResults;
            if (active == 0 && (!remind || temporary == 0)) return true;

            var decision = await _dialogs.ConfirmCloseAsync(active, remind ? temporary : 0, remind && temporary > 0);
            _dontRemind = decision.Close && decision.DontRemind && remind && temporary > 0;
            return decision.Close;
        }

        // Called only once every close guard has agreed. A cancelled editor close
        // must never cancel inference or change the reminder preference.
        public async Task CommitCloseAsync() {
            _style.IsClosing = _superResolution.IsClosing = true;
            var tasks = Tasks;
            foreach (var task in tasks) {
                task.IsClosing = true;
                task.Cts.Cancel();
            }
            if (_dontRemind) {
                _settings.Settings.RemindUnpreservedIntelligentResults = false;
                try { await _settings.SaveAsync<ISettings>().WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception ex) { ArcLog.GetLogger<IntelligentTaskCloseCoordinator>().Error(ex); }
            }
            try {
                await Task.WhenAll(tasks.SelectMany(t => new[] { t.Execution, t.Preservation })).WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) {
                // Native inference may not observe cancellation immediately. The UI
                // process is exiting; it must not keep its window open indefinitely.
            }
            catch (Exception ex) { ArcLog.GetLogger<IntelligentTaskCloseCoordinator>().Error(ex); }
        }
    }
}
