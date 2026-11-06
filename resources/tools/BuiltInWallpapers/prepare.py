"""Prepare CC0 nature footage and the interactive glow wallpaper without application UI."""
import argparse
import base64
import importlib.util
import io
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import threading
import time
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.request import Request, urlopen
import zipfile

from PIL import Image, ImageChops

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
OUTPUT=ROOT/"TestResults/built-in-wallpapers"
spec=importlib.util.spec_from_file_location("preview",ROOT/"resources/readme/generate-previews.py")
preview=importlib.util.module_from_spec(spec)
spec.loader.exec_module(preview)


def run(ffmpeg,*arguments):
    subprocess.run([ffmpeg,"-y","-v","error",*map(str,arguments)],check=True)


def motion_loop(ffmpeg,source,target,item):
    """Keep forward, real-time camera footage. Blend only the short loop join."""
    duration=item["durationSeconds"];blend=item["loopBlendSeconds"]
    offset=duration-2*blend
    base="fps=30,setpts=PTS-STARTPTS"
    if item.get("crop"):base+=",crop="+item["crop"]
    if item.get("scale"):base+=",scale="+item["scale"]+":flags=lanczos"
    filters=(f"[0:v]{base},split[a][b];[a]trim=start={blend},setpts=PTS-STARTPTS[body];"
             f"[b]trim=duration={blend},setpts=PTS-STARTPTS[head];"
             f"[body][head]xfade=transition=fade:duration={blend}:offset={offset:.6f},format=yuv420p[v]")
    run(ffmpeg,"-ss",item["startSeconds"],"-t",duration,"-i",source,"-filter_complex",filters,
        "-map","[v]","-an","-r","30","-c:v","libx264","-preset","medium","-crf","20",
        "-movflags","+faststart",target)


def prepare_motion(ffmpeg):
    manifest=json.loads((HERE/"media.json").read_text(encoding="utf-8"))
    cache=OUTPUT/"external";cache.mkdir(exist_ok=True)
    for item in manifest["media"]:
        source=cache/item["downloadName"]
        if not source.exists():
            partial=source.with_suffix(".download")
            with urlopen(Request(item["downloadUrl"],headers={"User-Agent":"VirtualPaper built-in wallpaper authoring"}),timeout=60) as response,partial.open("wb") as output:
                shutil.copyfileobj(response,output)
            partial.replace(source)
        with source.open("rb") as stream:
            actual=hashlib.file_digest(stream,"sha256").hexdigest()
        if actual!=item["sha256"]:raise ValueError(f"Source hash mismatch: {source}")
        target=OUTPUT/item["output"]
        if item["format"]=="GIF":
            loop=OUTPUT/(item["folder"]+"-loop.mp4")
            motion_loop(ffmpeg,source,loop,item);make_gif(ffmpeg,loop,target)
        else:motion_loop(ffmpeg,source,target,item)
        print(f"Prepared CC0 footage by {item['author']}: {target.name}",flush=True)


def make_gif(ffmpeg,video,path):
    # One shared palette and 50 ms cadence avoid palette shimmer and the former ~8 fps steps.
    raw=subprocess.check_output([ffmpeg,"-v","error","-i",str(video),"-vf","fps=20,scale=768:432:flags=lanczos",
                                 "-f","rawvideo","-pix_fmt","rgb24","-"])
    size=768*432*3
    frames=[Image.frombytes("RGB",(768,432),raw[i:i+size]) for i in range(0,len(raw),size)]
    sample=frames[::max(1,len(frames)//8)][:8]
    strip=Image.new("RGB",(768,432*len(sample)))
    for index,frame in enumerate(sample): strip.paste(frame,(0,432*index))
    palette=strip.quantize(colors=256)
    encoded=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in frames]
    encoded.append(encoded[0].copy())
    durations=[20]+[50]*(len(encoded)-2)+[30]
    encoded[0].save(path,save_all=True,append_images=encoded[1:],duration=durations,loop=0,optimize=True,disposal=1)
    with Image.open(path) as gif:
        first=gif.convert("RGB");gif.seek(gif.n_frames-1)
        assert ImageChops.difference(first,gif.convert("RGB")).getbbox() is None,"GIF loop endpoints differ"
    print(f"GIF: {len(encoded)} frames, 20 fps cadence, identical loop endpoints.",flush=True)


def prepare_web(browser_override):
    # A fresh staging directory prevents old forest assets entering the new Web package.
    web=OUTPUT/"luminous-web"
    web.mkdir(exist_ok=True)
    shutil.copyfile(HERE/"luminous-flow.html",web/"index.html")
    profile=OUTPUT/("headless-"+str(time.time_ns()));profile.mkdir()
    handler=lambda *a,**kw: SimpleHTTPRequestHandler(*a,directory=str(OUTPUT),**kw)
    server=ThreadingHTTPServer(("127.0.0.1",0),handler)
    threading.Thread(target=server.serve_forever,daemon=True).start()
    process=subprocess.Popen([preview.browser_path(browser_override),"--headless=new","--remote-debugging-port=0",
        "--remote-allow-origins=http://localhost",f"--user-data-dir={profile}","--no-first-run",
        "--no-default-browser-check","--disable-background-networking","--disable-extensions","--disable-sync",
        "--disable-component-update","--disable-breakpad","--disable-gpu","about:blank"],
        stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    browser=page=None
    try:
        port_file=profile/"DevToolsActivePort";deadline=time.monotonic()+20
        while not port_file.exists():
            if process.poll() is not None or time.monotonic()>deadline: raise RuntimeError("Private browser did not start")
            time.sleep(.1)
        port,endpoint=port_file.read_text().splitlines()[:2]
        browser=preview.Cdp(f"ws://127.0.0.1:{port}{endpoint}")
        target=browser.call("Target.createTarget",url=f"http://127.0.0.1:{server.server_port}/luminous-web/index.html")["targetId"]
        with urlopen(f"http://127.0.0.1:{port}/json/list",timeout=10) as response:
            info=next(t for t in json.load(response) if t["id"]==target)
        page=preview.Cdp(info["webSocketDebuggerUrl"])
        page.call("Page.bringToFront")
        page.call("Emulation.setEmulatedMedia",features=[dict(name="prefers-reduced-motion",value="no-preference")])
        page.call("Emulation.setDeviceMetricsOverride",width=960,height=540,deviceScaleFactor=1,mobile=False)
        deadline=time.monotonic()+15
        while not page.evaluate("typeof window.playbackChanged==='function'"):
            if time.monotonic()>deadline: raise RuntimeError("Web artwork did not load")
            time.sleep(.1)
        page.evaluate("window.ready")
        page.call("Input.dispatchMouseEvent",type="mouseMoved",x=300,y=260)
        page.call("Input.dispatchMouseEvent",type="mouseMoved",x=520,y=310)
        assert page.evaluate("particles.length")>0,"Pointer movement did not create light streams"
        before=page.evaluate("particles.length")
        page.call("Input.dispatchMouseEvent",type="mousePressed",x=520,y=310,button="left",clickCount=1)
        page.call("Input.dispatchMouseEvent",type="mouseReleased",x=520,y=310,button="left",clickCount=1)
        assert page.evaluate("particles.length")>before,"Click did not emit particles"
        time.sleep(.2)
        page.evaluate("window.pause()")
        frozen=page.evaluate("JSON.stringify([time,particles])")
        time.sleep(.15)
        assert page.evaluate("JSON.stringify([time,particles])")==frozen,"Pause changed the scene"
        raw=base64.b64decode(page.evaluate("canvas.toDataURL('image/png').split(',')[1]"))
        (OUTPUT/"luminous-flow-preview.png").write_bytes(raw)
        with Image.open(io.BytesIO(raw)) as image:
            image.thumbnail((480,270),Image.Resampling.LANCZOS);image.save(web/"preview.gif")
        page.evaluate("window.play()")
        deadline=time.monotonic()+3
        while page.evaluate("JSON.stringify([time,particles])")==frozen and time.monotonic()<deadline: time.sleep(.05)
        assert page.evaluate("JSON.stringify([time,particles])")!=frozen,"Resume did not advance the scene"
        page.call("Emulation.setEmulatedMedia",features=[dict(name="prefers-reduced-motion",value="reduce")])
        time.sleep(.1);frozen=page.evaluate("JSON.stringify([time,particles])");time.sleep(.15)
        assert page.evaluate("JSON.stringify([time,particles])")==frozen,"Reduced motion did not freeze the scene"
        print("Web: actual pointer/click emission, frozen pause, resume and reduced motion verified.",flush=True)
    finally:
        if page: page.socket.close()
        if browser:
            try: browser.call("Browser.close")
            except Exception: pass
            browser.socket.close()
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired: process.kill();process.wait(timeout=5)
        server.shutdown();server.server_close()
    metadata=dict(title="Luminous Flow",description="Cyan and violet light streams follow the pointer and bloom on click.",
        authors="VirtualPaper",tags="Built-in;内置;Abstract;Light;Interactive;Web",file="index.html",preview="preview.gif")
    (web/"project.json").write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding="utf-8")
    with zipfile.ZipFile(OUTPUT/"luminous-flow.zip","w",zipfile.ZIP_DEFLATED) as archive:
        for name in ("index.html","preview.gif","project.json"): archive.write(web/name,name)


def make_thumbnails(ffmpeg):
    for name,input_name in [("vp-alpine-stillness","alpine-lake.png"),("vp-emerald-morning","emerald-ridges.png"),
                            ("vp-luminous-flow","luminous-web/preview.gif")]:
        with Image.open(OUTPUT/input_name) as image:
            image.thumbnail((480,270),Image.Resampling.LANCZOS);image.convert("RGB").save(OUTPUT/(name+"-thumbnail.gif"))
    for name,input_name in [("vp-bigora-cascade","bigora-cascade.gif"),("vp-santa-rosa-waves","santa-rosa-waves.mp4")]:
        run(ffmpeg,"-i",OUTPUT/input_name,"-filter_complex",
            "[0:v]fps=15,scale=480:270:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=none",
            "-t","3","-loop","0",OUTPUT/(name+"-thumbnail.gif"))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg",required=True);parser.add_argument("--browser")
    parser.add_argument("--web-only",action="store_true");parser.add_argument("--thumbnails-only",action="store_true")
    parser.add_argument("--motion-only",action="store_true")
    args=parser.parse_args();OUTPUT.mkdir(parents=True,exist_ok=True)
    if args.thumbnails_only: make_thumbnails(args.ffmpeg);return
    if args.motion_only:prepare_motion(args.ffmpeg);make_thumbnails(args.ffmpeg);return
    prepare_web(args.browser)
    if args.web_only:return
    for name,installed in [("alpine-lake.png","vp-alpine-stillness/vp-alpine-stillness.png"),
                           ("emerald-ridges.png","vp-emerald-morning/vp-emerald-morning.png")]:
        if not (OUTPUT/name).exists():shutil.copyfile(ROOT/"resources/BuiltInWallpapers"/installed,OUTPUT/name)
    prepare_motion(args.ffmpeg)
    make_thumbnails(args.ffmpeg)
    print("CC0 camera footage prepared at real-time speed, with short blended loop joins.",flush=True)


if __name__=="__main__":main()
