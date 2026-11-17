"""Prepare local precision experiments; never modify the shipped models.

Requires numpy, Pillow, onnx==1.17.0, onnxconverter-common==1.14.0.
The optional --packages directory isolates conversion dependencies from Python's site packages.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import sys

parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path)
parser.add_argument("--packages", type=Path)
args = parser.parse_args()
if args.packages:
    sys.path.insert(0, str(args.packages.resolve()))
import numpy as np
import onnx
import onnxconverter_common
from onnxconverter_common import float16
from PIL import Image, ImageDraw

repo = Path(__file__).resolve().parents[2]
root = args.output.resolve()
root.mkdir(parents=True, exist_ok=True)
if (root / "manifest.json").exists():
    raise FileExistsError("Use a new result directory; preserve the previous experiment.")


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def describe(path, model):
    return {
        "path": str(path), "bytes": path.stat().st_size, "sha256": sha(path),
        "external_files": [{"path": str(sidecar), "bytes": sidecar.stat().st_size,
                            "sha256": sha(sidecar)} for sidecar in path.parent.glob(path.name + ".data")],
        "opsets": {item.domain or "ai.onnx": item.version for item in model.opset_import},
        "weight_types": dict(collections.Counter(onnx.TensorProto.DataType.Name(t.data_type)
                                                for t in model.graph.initializer)),
        "operations": dict(collections.Counter(t.op_type for t in model.graph.node)),
        "inputs": [str(t) for t in model.graph.input],
        "outputs": [str(t) for t in model.graph.output],
    }


models = {}
for kind, name in [("SuperResolution", "realesrgan_x4plus_dynamic.onnx"),
                   ("StyleTransfer", "adain_style_transfer.onnx"),
                   ("DepthEstimate", "depth_anything_v2_vits_dynamic.onnx")]:
    path = repo / "src/VirtualPaper.ML" / kind / "ai_models" / name
    model = onnx.load(path)
    models[kind] = describe(path, model)
    if kind == "SuperResolution":
        input_name = model.graph.input[0].name
        converted = float16.convert_float_to_float16(model, keep_io_types=True)
        onnx.checker.check_model(converted)
        variant = root / "realesrgan_x4plus_dynamic-fp16.onnx"
        onnx.save_model(converted, variant)
        models["SuperResolutionFp16"] = describe(variant, converted)
        models["SuperResolutionFp16"]["conversion"] = {
            "keep_io_types": True, "op_block_list": float16.DEFAULT_OP_BLOCK_LIST,
            "min_positive_val": 1e-7, "max_finite_val": 1e4,
        }
    del model

# A deterministic high-frequency/gradient fixture and an existing photographic test image.
# This is numerical comparison material, not new distributable wallpaper content.
image = Image.new("RGB", (512, 512))
y, x = np.mgrid[:512, :512]
pixels = np.stack((x / 511, y / 511, ((x // 8 + y // 8) % 2)), axis=-1)
image = Image.fromarray(np.uint8(pixels * 255))
draw = ImageDraw.Draw(image)
draw.ellipse((90, 70, 400, 380), outline="white", width=3)
draw.line((0, 511, 511, 0), fill="black", width=2)
draw.text((30, 230), "VirtualPaper 0123456789", fill="white")
photo_path = repo / "resources/readme/upscale-input-960.jpg"
with Image.open(photo_path) as photo:
    photo.load()
    photo = photo.convert("RGB")
    left, top = (photo.width - 512) // 2, (photo.height - 512) // 2
    photo = photo.crop((left, top, left + 512, top + 512))

cases = {}
for name, fixture in [("pattern", image), ("photo", photo)]:
    fixture.save(root / f"{name}.png")
    for size in [32, 512]:
        small = fixture if size == 512 else fixture.resize((size, size), Image.Resampling.LANCZOS)
        data = np.asarray(small, dtype=np.float32).transpose(2, 0, 1)[None] / 255
        path = root / f"{name}-{size}.f32"
        data.astype("<f4").tofile(path)
        cases[f"{name}-{size}"] = {
            "Name": f"{name}-{size}",
            "Inputs": [{"Name": input_name, "Path": str(path), "Shape": list(data.shape)}],
            "sha256": sha(path),
        }
manifest = {
    "tools": {"onnx": onnx.__version__, "onnxconverter_common": onnxconverter_common.__version__,
              "numpy": np.__version__},
    "models": models, "cases": cases,
    "photo_source": {"path": str(photo_path), "sha256": sha(photo_path)},
}
(root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(json.dumps({"manifest": str(root / "manifest.json"),
                  "original_bytes": models["SuperResolution"]["bytes"],
                  "fp16_bytes": models["SuperResolutionFp16"]["bytes"]}), flush=True)
