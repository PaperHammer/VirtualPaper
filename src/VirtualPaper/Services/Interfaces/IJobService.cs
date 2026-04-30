namespace VirtualPaper.Services.Interfaces {
    public interface IJobService {
        bool AddProcess(IntPtr processHandle);
        bool AddProcess(int processId);
        void Close();
        void Dispose();
    }
}
