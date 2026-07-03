using VirtualPaper.Common;

namespace VirtualPaper.Services.Interfaces {
    public interface IJobService {
        bool AddProcess(IntPtr processHandle);
        bool AddProcess(int processId);
        bool AddProcess(int processId, PluginName pluginName); 
        void StopPlugin(PluginName pluginName);
        void StopPlugin(int pid);
        void Close();
        void Dispose();

        /// <summary>
        /// Start a plugin, waiting asynchronously if the plugin is being updated.
        /// </summary>
        Task StartPluginAsync(PluginName pluginName, Func<Task> startAction, CancellationToken token = default);
    }
}
