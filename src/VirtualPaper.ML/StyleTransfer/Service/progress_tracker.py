import time
from dataclasses import dataclass, field


@dataclass
class ProgressTracker:
    total_tiles: int
    total_steps: int

    _start_time: float = field(default_factory=time.time, init=False)
    _tile_times: list = field(default_factory=list, init=False)
    _current_tile: int = field(default=0, init=False)
    _tile_start: float = field(default=0.0, init=False)

    def start_tile(self, tile_idx: int) -> None:
        self._current_tile = tile_idx
        self._tile_start   = time.time()

    def finish_tile(self) -> None:
        self._tile_times.append(time.time() - self._tile_start)

    @property
    def elapsed_ms(self) -> int:
        return int((time.time() - self._start_time) * 1000)

    @property
    def estimated_remaining_ms(self) -> int:
        done = len(self._tile_times)
        if done == 0:
            return -1
        avg = sum(self._tile_times) / done
        return int(avg * (self.total_tiles - done) * 1000)

    @property
    def progress_pct(self) -> float:
        done = len(self._tile_times)
        return done / self.total_tiles if self.total_tiles > 0 else 0.0
