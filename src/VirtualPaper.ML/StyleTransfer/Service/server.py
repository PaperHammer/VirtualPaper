import sys
import os
import math
import logging
import grpc
from concurrent import futures
from PIL import Image

import style_transfer_pb2 as pb
import style_transfer_pb2_grpc as pb_grpc
from progress_tracker import ProgressTracker
from tiled_pipeline   import TiledStyleTransfer

log = logging.getLogger(__name__)

# ── 常量 ────────────────────────────────────────────────────
STYLE_PROMPTS: dict[str, str] = {
    "OIL_PAINTING": "make it look like an oil painting, highly detailed brushstrokes", # 油画
    "WATER_COLOR": "make it look like a watercolor painting, soft edges", # 水彩
    "SKETCH": "make it look like a pencil sketch, black and white", # 素描
    "CYBERPUNK": "cyberpunk style, neon lights, futuristic city", # 赛博朋克
}

DEFAULT_STEPS                = 20
DEFAULT_IMAGE_GUIDANCE_SCALE = 1.5
DEFAULT_GUIDANCE_SCALE       = 7.5
TILE_SIZE                    = 512
TILE_OVERLAP                 = 64
GRPC_PORT                    = 50051


# ── Servicer ─────────────────────────────────────────────────
class StyleTransferServicer(pb_grpc.StyleTransferServicer):

    def __init__(self, pipeline, backend_info: dict):
        self.pipeline     = pipeline
        self.backend_info = backend_info
        self.tiled        = TiledStyleTransfer(pipeline)

    def GetCapability(self, request, context):
        return pb.CapabilityResponse(**self.backend_info)

    def StylizeWithProgress(self, request, context):
        try:
            yield from self._run(request)
        except Exception as e:
            log.exception("推理异常")
            yield pb.ProgressEvent(
                stage     = "error",
                error_msg = str(e),
                message   = f"推理失败: {e}",
            )

    def _run(self, request):
        # ── 1. 加载图片 ──────────────────────────────────────
        yield self._evt(stage="preparing", message="正在加载图片...", elapsed_ms=0)

        if not os.path.isfile(request.input_path):
            raise FileNotFoundError(f"输入文件不存在: {request.input_path}")

        image = Image.open(request.input_path).convert("RGB")
        w, h  = image.size
        log.info("图片尺寸: %dx%d", w, h)

        # ── 2. 分块计算 ──────────────────────────────────────
        stride      = TILE_SIZE - TILE_OVERLAP
        cols        = max(1, math.ceil((w - TILE_OVERLAP) / stride))
        rows        = max(1, math.ceil((h - TILE_OVERLAP) / stride))
        total_tiles = rows * cols

        steps  = request.steps or DEFAULT_STEPS
        img_gs = request.image_guidance_scale or DEFAULT_IMAGE_GUIDANCE_SCALE
        txt_gs = request.guidance_scale       or DEFAULT_GUIDANCE_SCALE
        prompt = STYLE_PROMPTS.get(request.style_name, request.style_name)

        tracker = ProgressTracker(total_tiles=total_tiles, total_steps=steps)

        yield self._evt(
            stage        = "tiling",
            total_tiles  = total_tiles,
            current_tile = 0,
            message      = f"图片 {w}×{h}，共 {total_tiles} 个分块",
            elapsed_ms   = tracker.elapsed_ms,
        )

        # ── 3. 逐 tile 推理 ──────────────────────────────────
        results = []

        for tile_idx, (y, x, tile_img) in enumerate(
            self.tiled.iter_tiles(image, TILE_SIZE, TILE_OVERLAP)
        ):
            tracker.start_tile(tile_idx)
            step_events = []

            def step_callback(pipe, step, timestep, kwargs,
                              _idx=tile_idx):          # 闭包捕获 tile_idx
                step_events.append(self._evt(
                    stage        = "denoising",
                    current_tile = _idx + 1,
                    total_tiles  = total_tiles,
                    current_step = step + 1,
                    total_steps  = steps,
                    elapsed_ms   = tracker.elapsed_ms,
                    estimated_ms = tracker.estimated_remaining_ms,
                    message      = f"分块 {_idx+1}/{total_tiles}，去噪步 {step+1}/{steps}",
                ))
                return kwargs

            result_tile = self.pipeline(
                prompt                             = prompt,
                image                              = tile_img,
                num_inference_steps                = steps,
                image_guidance_scale               = img_gs,
                guidance_scale                     = txt_gs,
                callback_on_step_end               = step_callback,
                callback_on_step_end_tensor_inputs = ["latents"],
            ).images[0]

            tracker.finish_tile()
            results.append((y, x, result_tile))

            for evt in step_events:
                yield evt

            yield self._evt(
                stage        = "tiling",
                current_tile = tile_idx + 1,
                total_tiles  = total_tiles,
                current_step = steps,
                total_steps  = steps,
                elapsed_ms   = tracker.elapsed_ms,
                estimated_ms = tracker.estimated_remaining_ms,
                message      = f"分块 {tile_idx+1}/{total_tiles} 完成",
            )

        # ── 4. 合并保存 ──────────────────────────────────────
        yield self._evt(stage="decoding", message="合并分块，保存图片...",
                        elapsed_ms=tracker.elapsed_ms)

        output = self.tiled.merge_tiles(results, image.size, TILE_SIZE, TILE_OVERLAP)

        # 确保输出目录存在
        os.makedirs(os.path.dirname(os.path.abspath(request.output_path)), exist_ok=True)
        output.save(request.output_path, format="PNG")
        log.info("输出保存: %s", request.output_path)

        yield self._evt(
            stage        = "done",
            current_tile = total_tiles,
            total_tiles  = total_tiles,
            elapsed_ms   = tracker.elapsed_ms,
            estimated_ms = 0,
            message      = f"完成，耗时 {tracker.elapsed_ms / 1000:.1f}s",
        )

    @staticmethod
    def _evt(**kwargs) -> pb.ProgressEvent:
        """构造 ProgressEvent，只传有值的字段"""
        return pb.ProgressEvent(**{k: v for k, v in kwargs.items() if v is not None})


# ── 启动 ─────────────────────────────────────────────────────
def serve(pipeline, backend_info: dict):
    server = grpc.server(
        futures.ThreadPoolExecutor(max_workers=1),
        options=[
            ("grpc.max_receive_message_length", 64 * 1024 * 1024),  # 64MB
            ("grpc.max_send_message_length",    64 * 1024 * 1024),
        ],
    )
    pb_grpc.add_StyleTransferServicer_to_server(
        StyleTransferServicer(pipeline, backend_info), server
    )
    server.add_insecure_port(f"localhost:{GRPC_PORT}")
    server.start()
    log.info("gRPC server 启动，端口 %d", GRPC_PORT)
    server.wait_for_termination()
