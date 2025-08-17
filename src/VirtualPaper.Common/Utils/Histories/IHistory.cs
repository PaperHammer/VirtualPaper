namespace VirtualPaper.Common.Utils.Histories {
    public interface IHistory<T1, T2> : IDisposable 
        where T1 : struct, IConvertible
        where T2 : struct, IConvertible {
        string Title { set; }
        T1 Mode { get; }
        T2 PropertyMode { get; }
    }
}
