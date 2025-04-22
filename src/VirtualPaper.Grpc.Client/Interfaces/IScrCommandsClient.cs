namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IScrCommandsClient {
        void AddToWhiteList(string procName);
        void ChangeLockStatu(bool isLock);
        void RemoveFromWhiteList(string procName);
        void Start();
        void Stop();
    }
}
