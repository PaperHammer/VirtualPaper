"""Render small README GIFs from VirtualPaper's actual atmosphere effect code.

Requires Python 3.10+, Pillow, websocket-client, and Chrome or Edge on Windows.
Only a private headless browser and a loopback HTTP server are started.
"""

import argparse
import base64
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import threading
import time
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.request import urlopen

from PIL import Image
import websocket


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path(__file__).resolve().parent
HTML = """<!doctype html><html lang="en"><meta charset="utf-8">
<link rel="icon" href="data:,"><title>VirtualPaper README previews</title>
<style>html,body{margin:0;background:#12132f;overflow:hidden}canvas{display:block}</style>
<canvas width="480" height="270"></canvas>
<script src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/effects/common.js"></script>
<script src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/effects/snow.js"></script>
<script src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/effects/fireflies.js"></script>
<script src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/effects/meteor.js"></script>
<script src="/src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/effects/petals.js"></script>
<script>
const canvas=document.querySelector('canvas'), ctx=canvas.getContext('2d');
const scene=new Image(); scene.src='/resources/readme/scene.svg';
window.ready=scene.decode();
let preset, particles, sprite, elapsed, style;
window.setup=(id, nextStyle)=>{
    let seed=29137;
    Math.random=()=>{seed=(Math.imul(1664525,seed)+1013904223)>>>0;return seed/4294967296;};
    preset=vpAtmosphereEffects.presets[id]; style=nextStyle; elapsed=0;
    particles=preset.createParticles(85,style); sprite=preset.createSprite?.(style);
};
window.draw=()=>{
    elapsed+=0.08; ctx.globalAlpha=1; ctx.drawImage(scene,0,0,480,270);
    preset.draw(ctx,particles,{dt:0.08,elapsed,width:480,height:270,
        scale:270/1080,opacity:0.3+85/160,style},sprite);
    ctx.globalAlpha=1;
    return canvas.toDataURL('image/png').split(',')[1];
};
</script></html>"""


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def do_GET(self):
        if self.path == "/__readme_preview__":
            body = HTML.encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            super().do_GET()

    def log_message(self, *_):
        pass


class Cdp:
    def __init__(self, url):
        self.socket = websocket.create_connection(url, timeout=30, suppress_origin=True)
        self.counter = 0

    def call(self, method, **params):
        self.counter += 1
        self.socket.send(json.dumps({"id": self.counter, "method": method, "params": params}))
        while True:
            response = json.loads(self.socket.recv())
            if response.get("id") == self.counter:
                if "error" in response:
                    raise RuntimeError(response["error"])
                return response.get("result", {})

    def evaluate(self, expression):
        result = self.call("Runtime.evaluate", expression=expression,
                           awaitPromise=True, returnByValue=True)
        if "exceptionDetails" in result:
            raise RuntimeError(result["exceptionDetails"])
        return result["result"].get("value")


def browser_path(value):
    candidates = [value] if value else [
        Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Google/Chrome/Application/chrome.exe",
        Path(os.environ.get("PROGRAMFILES(X86)", "C:/Program Files (x86)")) / "Microsoft/Edge/Application/msedge.exe",
    ]
    for path in candidates:
        if path and Path(path).is_file():
            return str(path)
    raise FileNotFoundError("Chrome/Edge not found; pass --browser <path>.")


def save_gif(frames, name):
    # Share one palette across frames to avoid color flicker. No dithering keeps
    # a static scene static and lets GIF encode only the changing particles.
    strip = Image.new("RGB", (480, 270 * 8))
    for i, frame in enumerate(frames[::8]):
        strip.paste(frame, (0, i * 270))
    palette = strip.quantize(colors=192, method=Image.Quantize.MEDIANCUT)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    target = OUTPUT / name
    encoded[0].save(target, save_all=True, append_images=encoded[1:],
                    duration=80, loop=0, optimize=True, disposal=1)
    print(f"{name}: {len(frames)} frames, 480x270, {target.stat().st_size / 1024:.0f} KiB", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--browser", help="Chrome or Edge executable")
    args = parser.parse_args()
    artifacts = ROOT / "TestResults/readme-refresh-20261104"
    artifacts.mkdir(parents=True, exist_ok=True)
    profile = tempfile.mkdtemp(prefix="headless-", dir=artifacts)
    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    process = subprocess.Popen([
        browser_path(args.browser), "--headless=new", "--remote-debugging-port=0",
        "--remote-allow-origins=http://localhost",
        f"--user-data-dir={profile}", "--no-first-run", "--no-default-browser-check",
        "--disable-background-networking", "--disable-extensions", "--disable-sync",
        "--disable-component-update", "--disable-breakpad", "--disable-gpu", "about:blank",
    ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
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
        browser = Cdp(f"ws://127.0.0.1:{port}{endpoint}")
        base = f"http://127.0.0.1:{server.server_port}"
        target = browser.call("Target.createTarget", url=base + "/__readme_preview__")["targetId"]
        with urlopen(f"http://127.0.0.1:{port}/json/list", timeout=10) as response:
            info = next(t for t in json.load(response) if t["id"] == target)
        page = Cdp(info["webSocketDebuggerUrl"])
        page.call("Emulation.setDeviceMetricsOverride", width=480, height=270,
                  deviceScaleFactor=1, mobile=False)
        deadline = time.monotonic() + 15
        while not page.evaluate("typeof window.draw==='function'"):
            if time.monotonic() > deadline:
                raise RuntimeError("Preview scripts did not load.")
            time.sleep(0.1)
        page.evaluate("window.ready")
        for preset, style, name in [(2, 1, "snow.gif"), (3, 0, "fireflies.gif"),
                                    (4, 0, "meteors.gif"), (6, 0, "petals.gif")]:
            page.evaluate(f"window.setup({preset},{style})")
            frames = []
            for _ in range(64):
                raw = base64.b64decode(page.evaluate("window.draw()"))
                frames.append(Image.open(io.BytesIO(raw)).convert("RGB"))
            save_gif(frames, name)
        sheet = Image.new("RGB", (960, 540))
        for i, name in enumerate(["snow.gif", "fireflies.gif", "meteors.gif", "petals.gif"]):
            with Image.open(OUTPUT / name) as gif:
                gif.seek(24)
                sheet.paste(gif.convert("RGB"), ((i % 2) * 480, (i // 2) * 270))
        sheet.save(artifacts / "gif-contact-sheet.png")
    finally:
        if page:
            page.socket.close()
        if browser:
            try:
                browser.call("Browser.close")
            except (OSError, websocket.WebSocketException):
                pass
            browser.socket.close()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    main()
