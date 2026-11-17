"""Render Store hero backgrounds and the first overview image; no foreground UI.

Requires Python 3.10+, Pillow, websocket-client, and Chrome or Edge on Windows.
Uses only a private headless browser and existing repository artwork/captures.
"""
import argparse
import base64
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import time
from urllib.request import urlopen

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ARTWORK = ROOT / "resources/store/artwork"
OVERVIEW = ROOT / "resources/store/screenshots/en-US/00-overview.png"


def image_uri(relative, frame=0):
    with Image.open(ROOT / relative) as image:
        image.seek(frame)
        output = io.BytesIO()
        image.convert("RGB").save(output, format="JPEG", quality=92)
    return "data:image/jpeg;base64," + base64.b64encode(output.getvalue()).decode("ascii")


def compose_hero():
    alpine = image_uri("resources/BuiltInWallpapers/vp-alpine-stillness/vp-alpine-stillness.png")
    emerald = image_uri("resources/BuiltInWallpapers/vp-emerald-morning/vp-emerald-morning.png")
    flow = image_uri("resources/BuiltInWallpapers/vp-luminous-flow/preview.gif")
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="3840" height="2160" viewBox="0 0 1920 1080">
  <title>VirtualPaper Store hero background</title>
  <desc>Original blue-violet composition of three bundled wallpapers, with softly focused layers, translucent paper folds and mist. No text or application UI.</desc>
  <defs>
    <linearGradient id="backdrop" x2="1" y2=".65"><stop stop-color="#19233a"/><stop offset=".55" stop-color="#304263"/><stop offset="1" stop-color="#343451"/></linearGradient>
    <radialGradient id="aqua"><stop stop-color="#7bcae0" stop-opacity=".35"/><stop offset="1" stop-color="#79bce1" stop-opacity="0"/></radialGradient>
    <radialGradient id="violet"><stop stop-color="#9d8be0" stop-opacity=".28"/><stop offset="1" stop-color="#9787df" stop-opacity="0"/></radialGradient>
    <linearGradient id="paper" x1=".1" y1="1" x2=".9" y2="0"><stop stop-color="#9995d5" stop-opacity="0"/><stop offset=".5" stop-color="#8ecce3" stop-opacity=".12"/><stop offset="1" stop-color="#c3e6ed" stop-opacity=".26"/></linearGradient>
    <linearGradient id="veil"><stop stop-color="#19233a"/><stop offset=".3" stop-color="#202e48" stop-opacity=".97"/><stop offset=".56" stop-color="#293955" stop-opacity=".72"/><stop offset=".78" stop-color="#26334f" stop-opacity=".21"/><stop offset="1" stop-color="#242c43" stop-opacity=".24"/></linearGradient>
    <linearGradient id="floor" x2="0" y2="1"><stop offset=".4" stop-color="#1b253a" stop-opacity="0"/><stop offset="1" stop-color="#182236" stop-opacity=".94"/></linearGradient>
    <filter id="focus" x="-12%" y="-15%" width="124%" height="130%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="3.4"/></filter>
    <filter id="mist" x="-40%" y="-60%" width="180%" height="220%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="42"/></filter>
    <filter id="shadow" x="-20%" y="-30%" width="140%" height="160%" color-interpolation-filters="sRGB"><feDropShadow dx="0" dy="22" stdDeviation="25" flood-color="#081023" flood-opacity=".6"/></filter>
    <clipPath id="flow"><rect width="680" height="382" rx="22"/></clipPath>
    <clipPath id="alpine"><rect width="820" height="462" rx="22"/></clipPath>
    <clipPath id="emerald"><rect width="1000" height="562" rx="22"/></clipPath>
  </defs>
  <rect width="1920" height="1080" fill="url(#backdrop)"/>
  <ellipse cx="1280" cy="300" rx="1000" ry="760" fill="url(#aqua)"/>
  <ellipse cx="1600" cy="940" rx="950" ry="680" fill="url(#violet)"/>
  <g opacity=".82" filter="url(#focus)" transform="translate(740 -95) rotate(-16 500 480)">
    <g filter="url(#shadow)"><image width="680" height="382" href="{flow}" clip-path="url(#flow)" preserveAspectRatio="xMidYMid slice"/></g>
    <g transform="translate(716 0)" filter="url(#shadow)"><image width="820" height="462" href="{alpine}" clip-path="url(#alpine)" preserveAspectRatio="xMidYMid slice"/></g>
    <g transform="translate(250 500)" filter="url(#shadow)"><image width="1000" height="562" href="{emerald}" clip-path="url(#emerald)" preserveAspectRatio="xMidYMid slice"/></g>
  </g>
  <rect width="1920" height="1080" fill="url(#veil)"/>
  <path d="M120 1080C740 890 775 80 1510-140C1200 330 1335 736 1920 895L1920 1080Z" fill="url(#paper)"/>
  <path d="M-120 1150C800 900 720 510 1220 260C1110 630 1360 880 1980 1030" fill="none" stroke="#b3dbea" stroke-opacity=".12" stroke-width="1.4"/>
  <ellipse cx="1110" cy="530" rx="490" ry="155" fill="#adcfe7" opacity=".08" filter="url(#mist)" transform="rotate(-23 1110 530)"/>
  <ellipse cx="1530" cy="860" rx="530" ry="140" fill="#9b86d4" opacity=".14" filter="url(#mist)"/>
  <rect width="1920" height="1080" fill="url(#floor)"/>
</svg>
'''
    path = ARTWORK / "store-hero.svg"
    path.write_text(svg, encoding="utf-8")
    return path


def render(browser_path):
    # Reuse the README renderer's CDP transport/browser discovery. No daily profile.
    spec = importlib.util.spec_from_file_location("vp_readme_renderer", ROOT / "resources/readme/generate-previews.py")
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    artifacts = ROOT / "TestResults/store-artwork"
    artifacts.mkdir(parents=True, exist_ok=True)
    profile = Path(tempfile.mkdtemp(prefix="headless-", dir=artifacts))
    log = (profile / "browser.log").open("w", encoding="utf-8")
    process = subprocess.Popen([
        helper.browser_path(browser_path), "--headless=new", "--remote-debugging-port=0",
        "--remote-allow-origins=http://localhost",
        f"--user-data-dir={profile}", "--no-first-run", "--no-default-browser-check",
        "--disable-background-networking", "--disable-extensions", "--disable-sync",
        "--disable-component-update", "--disable-breakpad", "--disable-gpu", "about:blank",
    ], stdout=subprocess.DEVNULL, stderr=log,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    browser = page = None
    try:
        port_file = profile / "DevToolsActivePort"
        deadline = time.monotonic() + 20
        while not port_file.exists():
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError("Headless browser did not start.")
            time.sleep(.1)
        port, endpoint = port_file.read_text().splitlines()[:2]
        browser = helper.Cdp(f"ws://127.0.0.1:{port}{endpoint}")
        target = browser.call("Target.createTarget", url="about:blank")["targetId"]
        with urlopen(f"http://127.0.0.1:{port}/json/list", timeout=10) as response:
            info = next(t for t in json.load(response) if t["id"] == target)
        page = helper.Cdp(info["webSocketDebuggerUrl"])
        page.call("Page.enable")
        # Preserve the SVG's transparent rounded corners instead of flattening
        # them onto the browser's default white page background.
        page.call("Emulation.setDefaultBackgroundColorOverride", color={"r": 0, "g": 0, "b": 0, "a": 0})
        for source, output, width, height in [
            (compose_hero(), ARTWORK / "store-hero-3840x2160.png", 3840, 2160),
            (ROOT / "resources/readme/hero.svg", OVERVIEW, 1920, 1200),
        ]:
            page.call("Emulation.setDeviceMetricsOverride", width=width, height=height, deviceScaleFactor=1, mobile=False)
            page.call("Page.navigate", url=source.as_uri())
            deadline = time.monotonic() + 15
            while not page.evaluate(f"location.href==={json.dumps(source.as_uri())} && document.readyState==='complete'"):
                if time.monotonic() > deadline:
                    raise RuntimeError(f"SVG did not load: {source}")
                time.sleep(.1)
            # Root dimensions are overridden only for export, not the source file.
            page.evaluate(f"document.documentElement.setAttribute('width','{width}');document.documentElement.setAttribute('height','{height}');new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))")
            raw = base64.b64decode(page.call("Page.captureScreenshot", format="png", captureBeyondViewport=False)["data"])
            with Image.open(io.BytesIO(raw)) as image:
                if image.size != (width, height):
                    raise RuntimeError(f"Unexpected raster dimensions: {image.size}")
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_bytes(raw)
            print(f"[OK] {output.relative_to(ROOT)}: {width} x {height}", flush=True)
        index_path = ROOT / "resources/store/screenshots/index.json"
        index = json.loads(index_path.read_text(encoding="utf-8-sig"))
        index["Scenes"]["00-overview"]["SourceSha256"] = hashlib.sha256(
            (ROOT / "resources/readme/hero.svg").read_bytes()).hexdigest()
        index_path.write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    finally:
        if page:
            page.socket.close()
        if browser:
            try:
                browser.call("Browser.close")
            except (OSError, RuntimeError, helper.websocket.WebSocketException):
                pass
            finally:
                browser.socket.close()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=5)
        log.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--browser", help="Chrome or Edge executable")
    render(parser.parse_args().browser)
