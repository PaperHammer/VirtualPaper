namespace VirtualPaper.Models {
    public class ProcInfo {
        public string ProcName { get; set; } = string.Empty;
        public string ProcPath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;

        public bool IsRunning { get; set; } = false;

        public ProcInfo() { }

        public ProcInfo(string procName, string procPath, string iconPath) {
            ProcName = procName;
            ProcPath = procPath;
            IconPath = iconPath;
        }
    }
}
