namespace VirtualPaper.ML.DepthEstimate.Interfaces;

/// <summary>创建由调用方释放的短期模型，不交给根 DI 容器跟踪。</summary>
public interface IDepthEstimateFactory {
    IDepthEstimate Create();
}
