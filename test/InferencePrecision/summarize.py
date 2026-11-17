"""Summarize raw timings and numerical differences, without running inference."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics

import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument("output", type=Path)
args = parser.parse_args()
root = args.output.resolve()
manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


results = {}
required = {f"smoke-{runtime}-{precision}-{backend}" for runtime in ["ui", "ml-test"]
            for precision, backend in [("fp32", "dml"), ("fp16", "dml"), ("fp16", "cpu")]}
required.update(f"timing-{case}-{precision}-{round_}" for case in ["pattern-512", "photo-512"]
                for precision in ["fp32", "fp16"] for round_ in [1, 2])
required.update(["timing-pattern-512-fp32-cpu", "timing-pattern-512-fp16-cpu",
                 "profile-512-fp32", "profile-512-fp16", "profile-cpu-fp16"])
for path in sorted(root.glob("*/result.json")):
    result = json.loads(path.read_text(encoding="utf-8"))
    monitoring = json.loads((path.parent / "monitor.json").read_text(encoding="utf-8"))
    request = json.loads((path.parent / "request.json").read_text(encoding="utf-8"))
    if result["Success"]:
        for sample in result["Samples"]:
            data = np.fromfile(sample["Output"], dtype="<f4")
            shape = request["Cases"][0]["Inputs"][0]["Shape"]
            expected = [shape[0], shape[1], shape[2] * 4, shape[3] * 4]
            if (sample["Shape"] != expected or data.size != math.prod(expected)
                    or not np.isfinite(data).all()):
                raise ValueError(f"Invalid output: {path}")
    results[path.parent.name] = {"result": result, "monitor": monitoring}
if missing := required - results.keys():
    raise RuntimeError(f"Missing measurements: {sorted(missing)}")

timings = {}
for case in ["pattern-512", "photo-512"]:
    for precision in ["fp32", "fp16"]:
        group = [results[f"timing-{case}-{precision}-{round_}"] for round_ in [1, 2]]
        if not all(item["result"]["Success"] and item["monitor"]["exit_code"] == 0 for item in group):
            raise RuntimeError(f"Incomplete timing group: {case}-{precision}")
        samples = [t for item in group for t in item["result"]["Samples"][0]["WarmMs"]]
        timings[f"{case}-{precision}"] = {
            "count": len(samples), "median_ms": statistics.median(samples),
            "min_ms": min(samples), "max_ms": max(samples),
            "session_load_ms": [item["result"]["LoadMs"] for item in group],
            "first_run_ms": [item["result"]["Samples"][0]["FirstRunMs"] for item in group],
            "peak_process_ws_bytes": max(item["result"]["PeakWorkingSetBytes"] for item in group),
            "gpu_peak_dedicated_bytes": max(item["monitor"]["gpu_peak_bytes"]["Dedicated"] for item in group),
            "gpu_peak_shared_bytes": max(item["monitor"]["gpu_peak_bytes"]["Shared"] for item in group),
        }


def compare(reference, candidate):
    ref_report = results[reference]["result"]["Samples"][0]
    new_report = results[candidate]["result"]["Samples"][0]
    if ref_report["Shape"] != new_report["Shape"]:
        raise ValueError(f"Output shape mismatch: {reference} vs {candidate}")
    ref = np.fromfile(ref_report["Output"], dtype="<f4")
    new = np.fromfile(new_report["Output"], dtype="<f4")
    expected = math.prod(ref_report["Shape"])
    if ref.size != expected or new.size != expected:
        raise ValueError("Output file length/shape mismatch")
    if not np.isfinite(ref).all() or not np.isfinite(new).all():
        raise ValueError(f"Non-finite output: {reference} vs {candidate}")
    error = new.astype(np.float64) - ref
    mse = float(np.mean(error * error))
    a = np.clip(ref * 255, 0, 255).astype(np.uint8)
    b = np.clip(new * 255, 0, 255).astype(np.uint8)
    error8 = np.abs(b.astype(np.int16) - a.astype(np.int16))
    return {"reference": reference, "candidate": candidate, "shape": ref_report["Shape"],
            "finite": True, "mae": float(np.mean(np.abs(error))),
            "max_abs": float(np.max(np.abs(error))), "mse": mse,
            "psnr_nominal_range_1_db": -10 * math.log10(mse) if mse else None,
            "rgb8_mae": float(error8.mean()), "rgb8_max": int(error8.max()),
            "rgb8_p99": float(np.percentile(error8, 99)),
            "rgb8_channels_over_2_fraction": float(np.mean(error8 > 2))}


differences = []
for case in ["pattern-512", "photo-512"]:
    differences.append(compare(f"timing-{case}-fp32-1", f"timing-{case}-fp16-1"))
    for precision in ["fp32", "fp16"]:
        differences.append(compare(f"timing-{case}-{precision}-1", f"timing-{case}-{precision}-2"))
differences.append(compare("timing-pattern-512-fp32-cpu", "timing-pattern-512-fp16-cpu"))
differences.append(compare("timing-pattern-512-fp32-cpu", "timing-pattern-512-fp32-1"))

placements = {}
for name, item in results.items():
    path = item["result"].get("Profile")
    if not path:
        continue
    events = json.loads(Path(path).read_text(encoding="utf-8"))
    providers = {}
    for event in events:
        provider = event.get("args", {}).get("provider")
        if provider:
            providers[provider] = providers.get(provider, 0) + 1
    placements[name] = providers
for name in ["profile-512-fp32", "profile-512-fp16"]:
    if not placements.get(name, {}).get("DmlExecutionProvider"):
        raise RuntimeError(f"No DirectML execution evidence: {name}")

source_hashes = {name: sha(item["path"]) == item["sha256"]
                 for name, item in manifest["models"].items()}
source_hashes.update({case: sha(item["Inputs"][0]["Path"]) == item["sha256"]
                      for case, item in manifest["cases"].items()})
for name, item in manifest["models"].items():
    for sidecar in item.get("external_files", []):
        source_hashes[f"{name}:{Path(sidecar['path']).name}"] = sha(sidecar["path"]) == sidecar["sha256"]
report = {"timings": timings, "differences": differences, "placements": placements,
          "source_hashes_match": source_hashes,
          "cases": {name: {"success": item["result"]["Success"], "error": item["result"]["Error"],
                           "native_version": item["result"]["NativeVersion"]} for name, item in results.items()}}
(root / "summary.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
if not all(source_hashes.values()) or not all(
        item["result"]["Success"] and item["monitor"]["exit_code"] == 0 and not item["monitor"]["timed_out"]
        for item in results.values()):
    raise SystemExit(1)
