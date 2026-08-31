# Depth Anything V2 Relative Depth

`DepthAnythingV2` is the preferred relative-depth implementation for the dynamic-image pipeline. It uses the general-purpose Depth Anything V2 ViT-S dynamic FP32 ONNX model.

- Model: `ai_models/depth_anything_v2_vits_dynamic.onnx`
- Input: `image`, FP32 RGB NCHW, dynamic batch/height/width
- Output: `depth`, FP32 `[batch,height,width]`
- Input height and width must be multiples of 14
- Runtime: ONNX Runtime CPU
- Precision: FP32; no FP16 or INT8 conversion

Preprocessing follows the model exporter and official Depth Anything V2 implementation:

1. Convert OpenCV BGR to RGB.
2. Resize with cubic interpolation.
3. Convert pixels to the 0..1 range.
4. Normalize with ImageNet RGB mean `[0.485,0.456,0.406]` and standard deviation `[0.229,0.224,0.225]`.
5. Convert to FP32 NCHW.

The default `FitLongestSide` mode preserves aspect ratio and limits CPU work. `FillShortestSide` matches the official lower-bound resize and retains more detail at a higher CPU cost. `StretchSquare` matches the ONNX release's simple fixed-square inference example.

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

The returned depth array is resized to the original image dimensions and normalized to `[0,1]`. Larger values represent regions estimated to be closer to the camera.

Sources:

- https://github.com/DepthAnything/Depth-Anything-V2
- https://github.com/fabio-sim/Depth-Anything-ONNX/releases/tag/v2.0.0
- https://github.com/fabio-sim/Depth-Anything-ONNX/blob/main/dynamo.py

The original Depth Anything V2 code is Apache-2.0. The converted ONNX release and model redistribution terms must remain in the product's third-party review.
