# RTMDet-Tiny Object Detection

This module uses the official MMDetection RTMDet-Tiny COCO checkpoint, converted with MMDeploy 1.3.1 to a dynamic-shape FP32 ONNX model.

- Model: `ai_models/rtmdet_tiny_dynamic_fp32.onnx`
- ONNX input: `input`, FP32 NCHW, dynamic batch/height/width
- ONNX outputs: `dets` (`[1,N,5]`) and `labels` (`[1,N]`, Int64)
- Classes: COCO 80
- Default application input: 640×640
- Runtime: ONNX Runtime CPU

The generated `deploy.json`, `detail.json`, and `pipeline.json` are preserved under `model_metadata/` for reproducibility. Only the ONNX model is copied to the runtime plugin directory.

Preprocessing follows the generated pipeline exactly:

1. Keep aspect ratio.
2. Place the resized BGR image at the top-left.
3. Pad the right and bottom with BGR `(114,114,114)`.
4. Keep BGR channel order (`to_rgb=false`).
5. Normalize with mean `(103.53,116.28,123.675)` and standard deviation `(57.375,57.12,58.395)`.
6. Convert to FP32 NCHW.

Sources:

- https://github.com/open-mmlab/mmdetection/tree/main/configs/rtmdet
- https://github.com/open-mmlab/mmdeploy
- https://download.openmmlab.com/mmdetection/v3.0/rtmdet/rtmdet_tiny_8xb32-300e_coco/rtmdet_tiny_8xb32-300e_coco_20220902_112414-78e30dcc.pth

MMDetection and MMDeploy are distributed under Apache-2.0. Product release should retain applicable license and notice material and complete the separate checkpoint/data redistribution review.
