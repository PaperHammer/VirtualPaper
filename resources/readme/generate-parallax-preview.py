"""Compare the production regular and depth parallax players in a README GIF.

Uses the checked-in photograph and production-model depth map. Requires the
same Python packages and browser as generate-previews.py; no application UI,
user browser profile, model download, or production JavaScript edits are used.
"""

import argparse
import base64
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import threading
import time
from urllib.request import urlopen

from PIL import Image, ImageChops, ImageStat


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
spec = importlib.util.spec_from_file_location("preview", HERE / "generate-previews.py")
preview = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preview)
WIDTH, HEIGHT, FRAMES, DURATION = 812, 253, 50, 100
PERIOD = FRAMES * DURATION / 1000

# Keep native RAF and CSS transitions: both production players run in real time.
ERRORS = """(() => {
    window.previewErrors=[];
    const original=console.error;
    console.error=(...args)=>{previewErrors.push(args.map(String).join(' '));original(...args);};
    window.addEventListener('error',e=>previewErrors.push(e.message));
    window.addEventListener('unhandledrejection',e=>previewErrors.push(String(e.reason)));
})();"""

HTML = """<!doctype html><html lang="en"><meta charset="utf-8">
<link rel="icon" href="data:,"><title>VirtualPaper depth parallax</title>
<style>
html,body{margin:0;overflow:hidden;background:#f6f8fa;color:#323b4b}
main{display:grid;grid-template-columns:400px 400px;gap:12px}
header{height:28px;display:flex;align-items:center;justify-content:center;
font:600 13px 'Segoe UI',sans-serif}
.view{position:relative;width:400px;height:225px}
iframe{border:0;width:1920px;height:1080px;display:block;
transform:scale(0.2083333333333333);transform-origin:top left}
.pointer{position:absolute;left:0;top:0;pointer-events:none;
filter:drop-shadow(0 1px 2px #0009)}
</style><main>
<section><header>Regular parallax</header><div class="view"><iframe id="regular"
src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/default.html"></iframe>
<svg class="pointer" width="18" height="22" viewBox="0 0 18 22"><path
d="M2 1 L2 17 L6.5 13.5 L10 20 L13 18.5 L9.5 12 L16 12 Z"
fill="white" stroke="#202734" stroke-width="1.2" stroke-linejoin="round"/></svg>
</div></section>
<section><header>Depth parallax · Standard</header><div class="view"><iframe id="moving"
src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/3d_depth_map.html?moving"></iframe>
<svg class="pointer" width="18" height="22" viewBox="0 0 18 22"><path
d="M2 1 L2 17 L6.5 13.5 L10 20 L13 18.5 L9.5 12 L16 12 Z"
fill="white" stroke="#202734" stroke-width="1.2" stroke-linejoin="round"/></svg>
</div></section></main>
<script>
window.setup=async()=>{
    const normal=regular.contentWindow,depth=moving.contentWindow;
    for(const player of [normal,depth]){
        const style=player.document.createElement('style');
        style.textContent='.fade-in{animation:none!important;opacity:1!important}';
        player.document.head.appendChild(style);
    }
    normal.resourceLoad('RImage','/resources/readme/parallax-source.jpg');
    normal.updateDimensions(1920,1080);
    normal.objectFitChanged(0);
    await normal.document.querySelector('img').decode();
    normal.startParallax();
    const three=await depth.eval("import('/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/3d_depth/three.module.min.js')");
    await new Promise((resolve,reject)=>{
            three.DefaultLoadingManager.onLoad=resolve;
            three.DefaultLoadingManager.onError=url=>reject(Error('Texture failed: '+url));
            depth.updateFit3D('cover');
            depth.setParallaxStrength(1);
            depth.setParallaxAutoDrift(false);
            depth.resourceLoad('/resources/readme/parallax-source.jpg','/resources/readme/parallax-depth.png');
        });
    depth.startParallax();
};
window.step=(seconds)=>{
    const x=(1+Math.sin(seconds*Math.PI*2/5)*0.9)*960;
    const y=(1-Math.cos(seconds*Math.PI*2/5)*0.65)*540;
    for(const player of [regular.contentWindow,moving.contentWindow])
        player.document.dispatchEvent(new player.MouseEvent('mousemove',
            {clientX:x,clientY:y,bubbles:true}));
    document.querySelectorAll('.pointer').forEach(p=>p.style.transform=`translate(${x/4.8}px,${y/4.8}px)`);
};
window.begin=()=>{
    const start=performance.now();
    const animate=now=>{window.step((now-start)/1000);requestAnimationFrame(animate);};
    requestAnimationFrame(animate);
};
</script></html>"""


class Handler(preview.Handler):
    def do_GET(self):
        if self.path == "/__readme_parallax__":
            body = HTML.encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            super().do_GET()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--browser", help="Chrome or Edge executable")
    args = parser.parse_args()
    artifacts = ROOT / "TestResults/readme-refresh-20261104/parallax"
    artifacts.mkdir(parents=True, exist_ok=True)
    profile = tempfile.mkdtemp(prefix="browser-", dir=artifacts)
    server = preview.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    browser_log = (artifacts / "browser.log").open("wb")
    process = subprocess.Popen([
        preview.browser_path(args.browser), "--headless=new", "--remote-debugging-port=0",
        f"--user-data-dir={profile}", "--no-first-run", "--no-default-browser-check",
        "--disable-background-networking", "--disable-extensions", "--disable-sync",
        "--disable-component-update", "--disable-breakpad", "about:blank",
    ], stdout=subprocess.DEVNULL, stderr=browser_log,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    browser = page = None
    try:
        port_file = Path(profile) / "DevToolsActivePort"
        deadline = time.monotonic() + 20
        while not port_file.exists():
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError("Headless browser did not start.")
            time.sleep(0.1)
        port, endpoint = port_file.read_text().splitlines()[:2]
        browser = preview.Cdp(f"ws://127.0.0.1:{port}{endpoint}")
        target = browser.call("Target.createTarget", url="about:blank")["targetId"]
        with urlopen(f"http://127.0.0.1:{port}/json/list", timeout=10) as response:
            info = next(t for t in json.load(response) if t["id"] == target)
        page = preview.Cdp(info["webSocketDebuggerUrl"])
        page.call("Emulation.setDeviceMetricsOverride", width=WIDTH, height=HEIGHT,
                  deviceScaleFactor=1, mobile=False)
        page.call("Page.enable")
        page.call("Page.addScriptToEvaluateOnNewDocument", source=ERRORS)
        page.call("Page.navigate", url=f"http://127.0.0.1:{server.server_port}/__readme_parallax__")
        deadline = time.monotonic() + 20
        while not page.evaluate("typeof window.setup==='function' && [...document.querySelectorAll('iframe')].every(f=>typeof f.contentWindow.resourceLoad==='function')"):
            if time.monotonic() > deadline:
                raise RuntimeError("Production player did not load.")
            time.sleep(0.1)
        page.evaluate("window.setup()")
        page.evaluate("window.begin()")
        # Warm up a full circle, retaining native depth smoothing and CSS transitions.
        started = time.monotonic()
        time.sleep(PERIOD)
        frames = []
        capture_times = []
        for index in range(FRAMES):
            delay = started + PERIOD + index * DURATION / 1000 - time.monotonic()
            if delay > 0:
                time.sleep(delay)
            capture_time = time.monotonic() - started - PERIOD
            if capture_time >= PERIOD:
                break
            capture_times.append(capture_time)
            raw = page.call("Page.captureScreenshot", format="jpeg", quality=95,
                            captureBeyondViewport=False)["data"]
            frames.append(Image.open(io.BytesIO(base64.b64decode(raw))).convert("RGB"))
        assert len(frames) >= 25, "Capture is too slow for a readable animation."
        # Preserve capture timing instead of speeding up a slow screenshot run.
        boundaries = [0] + [round(t * 100) * 10 for t in capture_times[1:]] + [round(PERIOD * 1000)]
        durations = [b - a for a, b in zip(boundaries, boundaries[1:])]
        assert all(d > 0 for d in durations), durations
        # Explicitly close the GIF with the exact first frame. Split its duration
        # across both ends so the repeated frame does not add a seam pause.
        first_duration = durations[0]
        durations[0] = max(10, (first_duration // 20) * 10)
        durations.append(first_duration - durations[0])
        frames.append(frames[0].copy())
        errors = page.evaluate("[...previewErrors,...regular.contentWindow.previewErrors,...moving.contentWindow.previewErrors]")
        if errors:
            raise RuntimeError(errors)
        # This strip excludes the pointer in both compared frames, so cursor
        # movement alone cannot satisfy the moving-scene assertion.
        motion = {}
        quarter = min(range(len(capture_times)), key=lambda i: abs(capture_times[i] - PERIOD / 4))
        three_quarters = min(range(len(capture_times)), key=lambda i: abs(capture_times[i] - PERIOD * 3 / 4))
        for name, left in (("regular", 0), ("depth", 412)):
            roi = (left + 80, 50, left + 320, 160)
            mean = ImageStat.Stat(ImageChops.difference(frames[quarter].crop(roi), frames[three_quarters].crop(roi))).mean
            assert sum(mean) / 3 > 1, (name, mean)
            motion[name] = mean
        samples = frames[::8]
        strip = Image.new("RGB", (WIDTH, HEIGHT * len(samples)))
        for index, frame in enumerate(samples):
            strip.paste(frame, (0, index * HEIGHT))
        palette = strip.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
        encoded = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
        target = HERE / "parallax.gif"
        encoded[0].save(target, save_all=True, append_images=encoded[1:],
                        duration=durations, loop=0, optimize=True, disposal=1)
        with Image.open(target) as gif:
            first = gif.convert("RGB")
            gif.seek(gif.n_frames - 1)
            assert ImageChops.difference(first, gif.convert("RGB")).getbbox() is None
        assert sum(durations) == round(PERIOD * 1000)
        sheet = Image.new("RGB", (WIDTH, HEIGHT * 3))
        for row, index in enumerate((0, quarter, three_quarters)):
            sheet.paste(frames[index], (0, row * HEIGHT))
        sheet.save(artifacts / "contact-sheet.png")
        frames[quarter].save(artifacts / "preview.png")
        evidence = dict(frames=len(frames), width=WIDTH, height=HEIGHT, durationsMs=durations,
                        bytes=target.stat().st_size,
                        meanMotionDifference=motion, runtimeErrors=errors,
                        players=["js/default/run.js", "js/3d_depth/run.js"],
                        playerViewport=[1920, 1080], firstAndLastFramesIdentical=True,
                        strength="Regular: unchanged 1.05 scale and 2-degree tilt; depth: Standard (1), 0.024",
                        captureTimes=capture_times,
                        trajectory="5-second synchronized mouse circle, native RAF/CSS transitions, up to 10 fps capture")
        (artifacts / "render.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
        print(json.dumps({key: evidence[key] for key in (
            "frames", "width", "height", "bytes", "runtimeErrors",
            "playerViewport", "firstAndLastFramesIdentical", "strength")}, indent=2), flush=True)
    finally:
        if page:
            page.socket.close()
        if browser:
            try:
                browser.call("Browser.close")
            except (OSError, preview.websocket.WebSocketException):
                pass
            browser.socket.close()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
        server.shutdown()
        server.server_close()
        browser_log.close()


if __name__ == "__main__":
    main()
