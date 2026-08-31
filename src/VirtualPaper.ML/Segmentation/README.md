# MobileSAM Segmentation

This module contains the FP32 ONNX models used to turn RTMDet object boxes into pixel-level masks.

- Image encoder: `ai_models/mobile_sam_encoder_fp32.onnx`
  - Input: `input_image`, FP32 NCHW `[1,3,1024,1024]`
  - Output: `image_embeddings`, FP32 `[1,256,64,64]`
- Prompt encoder and mask decoder: `ai_models/mobile_sam_decoder_fp32.onnx`
  - Inputs: image embeddings, point/box prompts, optional mask input, and original image size
  - Outputs: `masks`, `iou_predictions`, and `low_res_masks`
  - Exported with `--return-single-mask`
- Runtime: ONNX Runtime CPU
- Precision: FP32; no FP16 or INT8 conversion

The image encoder is intentionally fixed at 1024 x 1024 because MobileSAM uses SAM's fixed image embedding layout. For each source image, run the encoder once and reuse its embedding for every RTMDet box passed to the decoder.

The decoder model was exported with MobileSAM's official ONNX export script. The TinyViT image encoder was exported separately because the official script only exports the prompt encoder and mask decoder.

Basic use with RTMDet detections:

```csharp
using var detector = new RTMDet();
detector.LoadModel();

ObjectDetectionModelOutput detection = detector.Run(imagePath);
SegmentationBox[] boxes = detection.Detections
    .Take(20)
    .Select(SegmentationBox.FromDetection)
    .ToArray();

using var segmenter = new MobileSam();
segmenter.LoadModels();

SegmentationModelOutput segmentation = segmenter.Run(imagePath, boxes);
for (int index = 0; index < segmentation.Masks.Count; index++) {
    SegmentationMask mask = segmentation.Masks[index];
    // mask.Alpha is a row-major, original-image-size 8-bit alpha plane.
    SegmentationVisualization.SaveAlphaMask(mask, $"output/mask_{index}.png");
    SegmentationVisualization.SaveTransparentCutout(
        imagePath,
        mask,
        $"output/layer_{index}.png");
    SegmentationVisualization.SaveContourPreview(
        imagePath,
        mask,
        $"output/preview_{index}.png");
}
```

Sources:

- https://github.com/ChaoningZhang/MobileSAM
- https://github.com/ChaoningZhang/MobileSAM/blob/master/scripts/export_onnx_model.py
- https://github.com/ChaoningZhang/MobileSAM/blob/master/weights/mobile_sam.pt

MobileSAM is distributed under Apache-2.0. Product releases should retain the applicable license and attribution material.
