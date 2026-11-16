using VirtualPaper.ML.DepthEstimate.Interfaces;
using VirtualPaper.ML.Inference;

namespace VirtualPaper.ML.DepthEstimate;

public sealed class DepthEstimateFactory(InferenceDevicePolicy policy) : IDepthEstimateFactory {
    // 模型独立创建/释放，GPU 失败记录仍沿用本次 UI 会话共享的策略。
    public IDepthEstimate Create() => new DepthAnythingV2(policy);
}
