# Depth Anything V2 相对深度估计

[English](README.md) | [简体中文](README.zh-CN.md)

`DepthAnythingV2` 为壁纸库的 3D 深度壁纸功能实现 `IDepthEstimate`，使用通用的 Depth Anything V2 ViT-S 动态 FP32 ONNX 模型。旧的 MiDaS 实现及其测试已移除。

- 模型：`ai_models/depth_anything_v2_vits_dynamic.onnx`
- 输入：`image`，FP32 RGB NCHW，batch、高度和宽度均为动态维度
- 输出：`depth`，FP32 `[batch,height,width]`
- 输入高度和宽度必须为 14 的倍数
- 运行时：ONNX Runtime CPU
- 精度：FP32，不进行 FP16 或 INT8 转换

预处理遵循模型导出器及 Depth Anything V2 官方实现：

1. 将 OpenCV BGR 转为 RGB。
2. 使用三次插值调整尺寸。
3. 将像素转换到 0..1 范围。
4. 使用 ImageNet RGB 均值 `[0.485,0.456,0.406]` 和标准差 `[0.229,0.224,0.225]` 归一化。
5. 转为 FP32 NCHW。

默认的 `FitLongestSide` 模式保持宽高比并限制 CPU 开销。`FillShortestSide` 对应官方的 lower-bound 缩放方式，保留更多细节，但 CPU 开销也更高。`StretchSquare` 对应 ONNX 发布版本中简单的固定正方形推理示例。

```csharp
using var depthEstimator = new DepthAnythingV2();
depthEstimator.LoadModel();

DepthEstimateModelOutput depth = depthEstimator.Run(
    imagePath,
    new DepthAnythingOptions {
        InputSize = 518,
        ResizeMode = DepthAnythingResizeMode.FitLongestSide
    });

string previewPath = depthEstimator.SaveDepthMap(depth, outputFolder);
```

返回的深度数组会恢复到原图尺寸，并归一化到 `[0,1]`。值越大，表示模型估计该区域越靠近相机。

壁纸库通过 `DepthMapCache.GetOrCreateAsync` 在 UI 线程之外执行推理。请求共用可取消的推理门控。缓存 PNG 文件名包含由源图、ONNX 模型和预处理版本计算的 SHA-256 键。命中有效缓存时无需加载 ONNX 会话；未完成的结果不会对外提供。旧缓存文件会保留，因为已保存的壁纸布局可能仍在引用它们。

深度播放器按需渲染，在替换时释放资源，并限制图片边缘和深度突变处的位移。这能减轻采样伪影，但不会重建被遮挡的像素，也不会修正错误的深度预测。

参考来源：

- https://github.com/DepthAnything/Depth-Anything-V2
- https://github.com/fabio-sim/Depth-Anything-ONNX/releases/tag/v2.0.0
- https://github.com/fabio-sim/Depth-Anything-ONNX/blob/main/dynamo.py

Depth Anything V2 原始代码使用 Apache-2.0 许可证。转换后的 ONNX 发布版本及模型再分发条款仍需纳入产品的第三方审核。
