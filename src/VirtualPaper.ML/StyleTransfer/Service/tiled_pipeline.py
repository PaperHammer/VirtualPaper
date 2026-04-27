import math
from typing import Generator, List, Tuple
from PIL import Image
import numpy as np


class TiledStyleTransfer:

    def __init__(self, pipeline):
        self.pipeline = pipeline

    # ── 切块迭代器 ───────────────────────────────────────────
    def iter_tiles(
        self,
        image: Image.Image,
        tile_size: int,
        overlap: int,
    ) -> Generator[Tuple[int, int, Image.Image], None, None]:
        """yield (y, x, tile_image)"""
        w, h   = image.size
        stride = tile_size - overlap

        rows = max(1, math.ceil((h - overlap) / stride))
        cols = max(1, math.ceil((w - overlap) / stride))

        for row in range(rows):
            for col in range(cols):
                x = min(col * stride, max(0, w - tile_size))
                y = min(row * stride, max(0, h - tile_size))

                x2, y2 = min(x + tile_size, w), min(y + tile_size, h)
                tile   = image.crop((x, y, x2, y2))

                if tile.size != (tile_size, tile_size):
                    tile = self._pad_to(tile, tile_size)

                yield y, x, tile

    # ── 合并拼接 ─────────────────────────────────────────────
    def merge_tiles(
        self,
        results: List[Tuple[int, int, Image.Image]],
        original_size: Tuple[int, int],
        tile_size: int,
        overlap: int,
    ) -> Image.Image:
        w, h   = original_size
        canvas = np.zeros((h, w, 3), dtype=np.float32)
        weight = np.zeros((h, w, 1), dtype=np.float32)
        fade   = self._make_fade_mask(tile_size, overlap)

        for y, x, tile_img in results:
            tile_np = np.array(tile_img.convert("RGB"), dtype=np.float32)
            h_end   = min(y + tile_size, h)
            w_end   = min(x + tile_size, w)
            th, tw  = h_end - y, w_end - x

            canvas[y:h_end, x:w_end] += tile_np[:th, :tw] * fade[:th, :tw, np.newaxis]
            weight[y:h_end, x:w_end] += fade[:th, :tw, np.newaxis]

        weight = np.maximum(weight, 1e-6)
        merged = np.clip(canvas / weight, 0, 255).astype(np.uint8)
        return Image.fromarray(merged)

    # ── 工具方法 ─────────────────────────────────────────────
    @staticmethod
    def _pad_to(tile: Image.Image, size: int) -> Image.Image:
        padded = Image.new("RGB", (size, size))
        padded.paste(tile, (0, 0))
        tw, th = tile.size
        if tw < size:
            strip = tile.crop((tw - 1, 0, tw, th)).resize((size - tw, th), Image.BILINEAR)
            padded.paste(strip, (tw, 0))
        if th < size:
            strip = padded.crop((0, th - 1, size, th)).resize((size, size - th), Image.BILINEAR)
            padded.paste(strip, (0, th))
        return padded

    @staticmethod
    def _make_fade_mask(tile_size: int, overlap: int) -> np.ndarray:
        mask = np.ones((tile_size, tile_size), dtype=np.float32)
        if overlap <= 0:
            return mask
        ramp = np.linspace(0, 1, overlap, dtype=np.float32)
        mask[:, :overlap]  *= ramp[np.newaxis, :]
        mask[:, -overlap:] *= ramp[::-1][np.newaxis, :]
        mask[:overlap, :]  *= ramp[:, np.newaxis]
        mask[-overlap:, :] *= ramp[::-1][:, np.newaxis]
        return mask
