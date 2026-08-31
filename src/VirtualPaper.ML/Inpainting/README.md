# MI-GAN background inpainting

`MIGan` runs the official MI-GAN-512 Places2 ONNX pipeline using the CPU
execution provider. The bundled pipeline accepts an RGB `uint8` image and a
single-channel `uint8` mask. In the model mask, 255 means a known pixel and 0
means an area to reconstruct; `MIGan` converts the dynamic-image module's
opposite mask convention automatically.

The object mask is dilated before inference to remove foreground-colored edge
pixels from the repaired background. `MaskExpansionPixels` defaults to 8 and
can be adjusted through `DynamicImageAnalysisOptions.Inpainting`.

Model source:

- <https://huggingface.co/andraniksargsyan/migan/blob/main/migan_pipeline_v2.onnx>
- <https://github.com/Picsart-AI-Research/MI-GAN>

MI-GAN is distributed under the MIT license. Keep the upstream license and
attribution with packaged model assets.
