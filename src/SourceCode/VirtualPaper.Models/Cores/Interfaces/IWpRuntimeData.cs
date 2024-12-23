using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces {
    public interface IWpRuntimeData {
        ApplicationInfo AppInfo { get; set; }
        string MonitorContent { get; set; }
        string FolderPath { get; set; }
        string DepthFilePath { get; set; }

        /// <summary>
        /// 壁纸运行时自定义配置文件存储位置
        /// </summary>
        string WpEffectFilePathTemplate { get; set; } // 还原到最初
        string WpEffectFilePathUsing { get; set; } // 还原到当前的应用设置
        string WpEffectFilePathTemporary { get; set; } // 实时调整与预览
        RuntimeType RType { get; set; }

        void RevertToDefault();
        void RevertToApplied();
        void Read(string filePath);
        void MoveTo(string targetFolder);
        void FromTempToInstallPath(string targetFolderPath);
        void Save();
        IWpRuntimeData Clone();
        bool IsAvailable();
    }
}
