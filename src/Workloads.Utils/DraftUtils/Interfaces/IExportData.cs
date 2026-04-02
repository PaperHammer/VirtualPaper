namespace Workloads.Utils.DraftUtils.Interfaces {
    /// <summary>
    /// 导出数据的基接口，所有具体项目的导出配置数据都要实现此接口
    /// </summary>
    public interface IExportData {
        public string Name { get; }
        public string Path { get; }
        public int Count { get; }
    }
}
