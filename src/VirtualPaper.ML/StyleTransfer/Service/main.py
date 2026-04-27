import sys
import os

# ── 路径注入 ──────────────────────────────────
_SERVICE_DIR = os.path.dirname(os.path.abspath(__file__))          # .../StyleTransfer/Service
_STYLE_DIR   = os.path.dirname(_SERVICE_DIR)                       # .../StyleTransfer
_PKG_DIR     = os.path.join(_STYLE_DIR, "env", "packages")        # .../StyleTransfer/env/packages
_PROTO_DIR   = os.path.join(_STYLE_DIR, "Protos")                 # .../StyleTransfer/Protos
_MODEL_DIR   = os.path.join(_STYLE_DIR, "ai_models")              # .../StyleTransfer/ai_models

for p in [_PKG_DIR, _SERVICE_DIR, _PROTO_DIR]:
    if p not in sys.path:
        sys.path.insert(0, p)
# ────────────────────────────────────────────────────────────

import torch
import logging
from server import serve

logging.basicConfig(
    level   = logging.INFO,
    format  = "[%(asctime)s] %(levelname)s %(message)s",
    datefmt = "%H:%M:%S",
)
log = logging.getLogger(__name__)


def load_pipeline(model_dir: str):
    """加载 InstructPix2Pix 风格化管线（CPU 模式）"""
    from diffusers import StableDiffusionInstructPix2PixPipeline

    log.info("加载模型: %s", model_dir)

    pipe = StableDiffusionInstructPix2PixPipeline.from_pretrained(
        model_dir,
        torch_dtype    = torch.float32,   # CPU 只能用 float32
        safety_checker = None,
        local_files_only = True,          # 只用本地，不联网
    )
    pipe.to("cpu")

    # CPU 推理优化
    pipe.enable_attention_slicing()       # 减少内存占用

    log.info("模型加载完成，运行在 CPU")
    return pipe


def main():
    pipe = load_pipeline(_MODEL_DIR)

    backend_info = {
        "backend"       : "cpu",
        "vram_mb"       : 0,
        "max_tile_size" : 512,
        "supports_lcm"  : False,
    }

    serve(pipe, backend_info)


if __name__ == "__main__":
    main()
