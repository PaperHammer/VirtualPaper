# Dynamic image analysis

This module combines four local FP32 ONNX pipelines shipped by
`VirtualPaper.ML`:

1. RTMDet-Tiny finds candidate objects.
2. MobileSAM converts each selected detection box into a pixel mask.
3. Depth Anything V2 estimates normalized relative depth, where larger values
   are treated as nearer to the viewer.
4. Highly-contained masks of the same detected class are merged to remove
   nested duplicate detections.
5. `LayerFusionEngine` creates background, midground, foreground and object
   layers in back-to-front order.
6. MI-GAN expands the accepted object mask slightly and reconstructs the
   background pixels hidden by those objects.

Each object layer contains both `SourceAlpha` (its complete segmentation mask)
and `VisibleAlpha` (the part not hidden by a nearer object). `InpaintingMask`
is the union of all accepted object masks. MI-GAN uses it to produce the
full-resolution `BackgroundPlate`; object layers retain pixels from the source
image while scene layers use this repaired plate.

```csharp
using var analyzer = new DynamicImageAnalyzer();
analyzer.LoadModels();

DynamicImageAnalysisResult result = analyzer.Analyze(imagePath);
foreach (DynamicImageLayer layer in result.LayerPlan.BackToFrontLayers) {
    // Build textures/cutouts from layer.VisibleAlpha and render back-to-front.
}

DynamicImageExportResult exported = DynamicImageExporter.Export(
    imagePath,
    result,
    outputDirectory);
```

The export directory contains `manifest.json`, render and raw depth maps,
`background_plate.png`, `inpainting_mask.png`, a color-coded
`inpainting_mask_applied.png`, a color-coded `layer_order_preview.png`, and one
folder per layer with complete/visible alpha masks and transparent PNG cutouts.
The first mask is the fused object mask; the applied mask includes selective
hole filling and resolution-aware expansion and is the mask actually sent to
MI-GAN. The manifest records its conservative parallax safety margin.

The exporter also writes `background_depth.png`, a depth field with detected
objects removed, and creates `dynamic_wallpaper.zip`. The package is compatible
with VirtualPaper's standard web-wallpaper preview path. Its WebGL renderer
uses the repaired depth field for continuous background parallax and overlays
transparent object layers with mouse and idle motion bounded by the recorded
safety margin.

The current output is a layer plan, not a Live2D model file. The next stages
can use these layers for depth parallax, localized deformation and background
animation.
