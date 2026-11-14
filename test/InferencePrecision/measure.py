"""Run a prepared SR graph in fresh headless native-ORT processes.

GPU counters are per process; export allocations are excluded by the worker's output marker.
No application, installed profile, foreground window or input device is used.
"""
import argparse
import ctypes as ct
from ctypes import wintypes as wt
import json
from pathlib import Path
import queue
import subprocess
import threading
import time

import psutil

parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path)
parser.add_argument("--phase", choices=["smoke", "timing", "profile"], required=True)
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
root = args.output.resolve()
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
worker = repo / "test/InferencePrecision/bin/Release/net10.0/InferencePrecision.dll"
ui = repo / "src/VirtualPaper/bin/Release/net10.0-windows10.0.19041.0/Plugins/UI"
test = repo / "src/VirtualPaper.ML.Test/bin/Release/net10.0-windows10.0.19041.0"
runtimes = {"ui": (ui / "onnxruntime.dll", ui / "DirectML.dll"),
            "ml-test": (test / "runtimes/win-x64/native/onnxruntime.dll", test / "DirectML.dll")}


class ValueUnion(ct.Union):
    _fields_ = [("large", ct.c_longlong), ("double", ct.c_double), ("long", wt.LONG)]


class Value(ct.Structure):
    _fields_ = [("status", wt.DWORD), ("value", ValueUnion)]


class Item(ct.Structure):
    _fields_ = [("name", wt.LPWSTR), ("value", Value)]


class GpuMemory:
    def __init__(self):
        self.api = ct.WinDLL("pdh")
        self.query = wt.HANDLE()
        self.counters = {}
        self.error = None
        self.api.PdhOpenQueryW.argtypes = [wt.LPCWSTR, ct.c_size_t, ct.POINTER(wt.HANDLE)]
        self.api.PdhAddEnglishCounterW.argtypes = [wt.HANDLE, wt.LPCWSTR, ct.c_size_t, ct.POINTER(wt.HANDLE)]
        self.api.PdhCollectQueryData.argtypes = [wt.HANDLE]
        self.api.PdhGetFormattedCounterArrayW.argtypes = [wt.HANDLE, wt.DWORD, ct.POINTER(wt.DWORD),
                                                         ct.POINTER(wt.DWORD), ct.c_void_p]
        self.api.PdhCloseQuery.argtypes = [wt.HANDLE]
        try:
            if self.api.PdhOpenQueryW(None, 0, ct.byref(self.query)):
                raise RuntimeError("PdhOpenQuery failed")
            for name in ["Dedicated", "Shared"]:
                handle = wt.HANDLE()
                path = f"\\GPU Process Memory(*)\\{name} Usage"
                if self.api.PdhAddEnglishCounterW(self.query, path, 0, ct.byref(handle)):
                    raise RuntimeError(f"Cannot add {path}")
                self.counters[name] = handle
        except Exception as ex:
            self.error = str(ex)

    def read(self, pid):
        if self.error:
            return {}
        if self.api.PdhCollectQueryData(self.query):
            return {}
        result = {}
        for name, handle in self.counters.items():
            size, count = wt.DWORD(), wt.DWORD()
            self.api.PdhGetFormattedCounterArrayW(handle, 0x400, ct.byref(size), ct.byref(count), None)
            if not size.value:
                continue
            buffer = ct.create_string_buffer(size.value)
            if self.api.PdhGetFormattedCounterArrayW(handle, 0x400, ct.byref(size), ct.byref(count), buffer):
                continue
            items = ct.cast(buffer, ct.POINTER(Item))
            matches = [items[i] for i in range(count.value)
                       if items[i].name.startswith(f"pid_{pid}_") and items[i].value.status in (0, 1)]
            if matches:
                result[name] = sum(item.value.value.large for item in matches)
        return result

    def close(self):
        if self.query:
            self.api.PdhCloseQuery(self.query)


def run(label, runtime, precision, backend, case, runs=3, profile=False):
    output = root / label
    if output.exists():
        raise FileExistsError(f"Preserve old evidence: {output}")
    output.mkdir()
    native, dml = runtimes[runtime]
    request = {"Model": manifest["models"]["SuperResolution" + ("Fp16" if precision == "fp16" else "")]["path"],
               "Runtime": str(native), "DirectML": str(dml), "Backend": backend,
               "OutputDirectory": str(output), "Runs": runs, "Profile": profile,
               "Cases": [manifest["cases"][case]]}
    request_path = output / "request.json"
    request_path.write_text(json.dumps(request, indent=2), encoding="utf-8")
    messages = queue.Queue()
    monitor = GpuMemory()
    peak_rss, peaks, matched_samples = 0, {}, 0
    begin = time.monotonic()
    process = subprocess.Popen(["dotnet", str(worker), str(request_path)], stdout=subprocess.PIPE,
                               stderr=(output / "stderr.log").open("w", encoding="utf-8"), text=True,
                               creationflags=subprocess.CREATE_NO_WINDOW)
    def read_lines():
        for line in process.stdout:
            messages.put(line)
    reader = threading.Thread(target=read_lines, daemon=True)
    reader.start()
    measured = True
    timed_out = False
    lines = []
    try:
        while process.poll() is None:
            while not messages.empty():
                line = messages.get_nowait()
                lines.append(line)
                if '"Stage":"output"' in line:
                    measured = False
            if measured:
                try:
                    peak_rss = max(peak_rss, psutil.Process(process.pid).memory_info().rss)
                except psutil.NoSuchProcess:
                    pass
                values = monitor.read(process.pid)
                if values:
                    matched_samples += 1
                for name, value in values.items():
                    peaks[name] = max(peaks.get(name, 0), value)
            if time.monotonic() - begin > 240:
                timed_out = True
                process.kill()
                break
            time.sleep(0.1)
        process.wait()
        reader.join(timeout=2)
        while not messages.empty():
            lines.append(messages.get_nowait())
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        monitor.close()
    (output / "stdout.log").write_text("".join(lines), encoding="utf-8")
    monitoring = {"exit_code": process.returncode, "timed_out": timed_out,
                  "peak_working_set_sampled_bytes": peak_rss, "gpu_peak_bytes": peaks,
                  "gpu_matched_samples": matched_samples, "gpu_counter_error": monitor.error,
                  "interval_ms": 100, "wall_seconds": time.monotonic() - begin}
    (output / "monitor.json").write_text(json.dumps(monitoring, indent=2), encoding="utf-8")
    path = output / "result.json"
    result = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
    print(json.dumps({"label": label, "success": result.get("Success", False),
                      "load_ms": result.get("LoadMs"), "samples": result.get("Samples"),
                      "error": result.get("Error"), "monitor": monitoring}), flush=True)
    return result.get("Success", False)


if args.phase == "smoke":
    for runtime in runtimes:
        for precision, backend in [("fp32", "dml"), ("fp16", "dml"), ("fp16", "cpu")]:
            run(f"smoke-{runtime}-{precision}-{backend}", runtime, precision, backend, "pattern-32", 1)
elif args.phase == "timing":
    # Fresh processes alternate A/B then B/A, with one untimed first run per process.
    for case in ["pattern-512", "photo-512"]:
        for round_, order in [(1, ["fp32", "fp16"]), (2, ["fp16", "fp32"])]:
            for precision in order:
                run(f"timing-{case}-{precision}-{round_}", "ui", precision, "dml", case)
    run("timing-pattern-512-fp32-cpu", "ui", "fp32", "cpu", "pattern-512")
    run("timing-pattern-512-fp16-cpu", "ui", "fp16", "cpu", "pattern-512")
else:
    for precision in ["fp32", "fp16"]:
        run(f"profile-512-{precision}", "ui", precision, "dml", "pattern-512", 1, True)
    run("profile-cpu-fp16", "ui", "fp16", "cpu", "pattern-32", 1, True)
