using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.IntelligentPanel.Services;
using VirtualPaper.ML.Inference;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.IntelligentPanel.Models {
    /// <summary>
    /// 智能任务的公共状态：跟踪推理、结果留存和关闭流程，不负责具体的模型或文件操作。
    /// </summary>
    public abstract class IntelligentTaskItem : ObservableObject {
        // 取消排队或推理；导出和入库通过 Preservation 跟踪，关闭时等待其收尾。
        public CancellationTokenSource Cts { get; } = new();
        // CompletedTask 仅表示尚无操作，不代表图片已经处理完成。
        internal Task Execution { get; set; } = Task.CompletedTask;
        internal Task Preservation { get; private set; } = Task.CompletedTask;
        // 这些标记在 UI 线程上读写，用于阻止删除、关闭期间开始新的留存操作。
        internal bool IsRemoving { get; set; }
        internal bool IsClosing { get; set; }
        public abstract bool IsPending { get; }
        public abstract bool IsProcessing { get; }
        public abstract bool IsCompleted { get; }
        protected abstract void NotifyCommandsCanExecuteChanged();

        // 关闭硬件加速时隐藏设备状态；多阶段任务累计设备，并保留故障回退记录。
        public bool HasBackendInfo => _hasBackendInfo;
        public string BackendText => !_hasBackendInfo ? string.Empty : LanguageUtil.GetI18n(
            _usedCpu && _usedGpu ? (_usedCpuFallback ? Constants.I18n.Intelligent_Backend_MixedCpuFallback : Constants.I18n.Intelligent_Backend_Mixed) :
            _usedCpuFallback ? Constants.I18n.Intelligent_Backend_CpuFallback :
            _usedCpu ? Constants.I18n.Intelligent_Backend_Cpu : Constants.I18n.Intelligent_Backend_Gpu);

        internal IDisposable? ObserveInference(object model) => model is IInferenceStatusSource source
            ? new InferenceStatusSubscription(source, UpdateBackend, action => CrossThreadInvoker.InvokeOnUIThread(action)) : null;

        private void UpdateBackend(InferenceState state) {
            if (!state.HardwareAccelerationEnabled || state.Device == InferenceDevice.Uninitialized) return;
            bool cpu = _usedCpu || state.Device == InferenceDevice.Cpu;
            bool gpu = _usedGpu || state.Device == InferenceDevice.DirectML;
            bool fallback = _usedCpuFallback || state.IsCpuFallback;
            if (_hasBackendInfo && cpu == _usedCpu && gpu == _usedGpu && fallback == _usedCpuFallback) return;
            _hasBackendInfo = true;
            _usedCpu = cpu;
            _usedGpu = gpu;
            _usedCpuFallback = fallback;
            OnPropertyChanged(nameof(HasBackendInfo));
            RefreshBackend();
        }

        internal void RefreshBackend() => OnPropertyChanged(nameof(BackendText));

        // 记录本次任务是否成功导出或入库过；后续操作取消、失败时保留已有成功标记。
        public bool IsExported { get; private set; }
        public bool IsImported { get; private set; }
        public bool IsPreserving { get; private set; }
        // 处理完成并不等于结果已留存：两种方式都未成功时，仍属于需要提醒的临时结果。
        public bool IsTemporaryResult => IsCompleted && !IsExported && !IsImported;
        public string PreservationText => !IsCompleted ? string.Empty : LanguageUtil.GetI18n(
            IsExported && IsImported ? Constants.I18n.Intelligent_Result_ExportedAndImported :
            IsExported ? Constants.I18n.Intelligent_Result_Exported :
            IsImported ? Constants.I18n.Intelligent_Result_Imported : Constants.I18n.Intelligent_Result_Temporary);

        /// <summary>
        /// 在 UI 线程上启动导出或入库。已有操作时共用其 Task，不重复启动，也不排队执行新请求。
        /// </summary>
        /// <param name="save">实际操作；仅返回 true 时才更新留存标记。</param>
        /// <param name="import">true 表示入库，false 表示导出文件。</param>
        internal Task PreserveAsync(Func<Task<bool>> save, bool import) {
            if (IsRemoving || IsClosing) return Task.CompletedTask;
            if (IsPreserving) return Preservation;
            return Preservation = PreserveCoreAsync(save, import);
        }

        private async Task PreserveCoreAsync(Func<Task<bool>> save, bool import) {
            // 操作期间禁用保存、入库和删除，避免重复请求或提前删除源文件。
            IsPreserving = true;
            OnPropertyChanged(nameof(IsPreserving));
            NotifyCommandsCanExecuteChanged();
            try {
                if (!await save()) return;
                if (import) IsImported = true;
                else IsExported = true;
                RefreshPreservation();
            }
            finally {
                // 取消、失败或抛出异常都要恢复按钮；异常继续交给调用方处理。
                IsPreserving = false;
                OnPropertyChanged(nameof(IsPreserving));
                NotifyCommandsCanExecuteChanged();
            }
        }

        internal void RefreshPreservation() {
            // 状态完成、留存成功或语言切换后，计算属性也需要主动通知界面刷新。
            OnPropertyChanged(nameof(IsExported));
            OnPropertyChanged(nameof(IsImported));
            OnPropertyChanged(nameof(IsTemporaryResult));
            OnPropertyChanged(nameof(PreservationText));
        }

        private bool _hasBackendInfo;
        private bool _usedCpu;
        private bool _usedGpu;
        private bool _usedCpuFallback;
    }
}
