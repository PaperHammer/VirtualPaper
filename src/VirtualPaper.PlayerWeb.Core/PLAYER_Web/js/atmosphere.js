// A viewport overlay shared by image, video, depth-image and iframe wallpapers.
// Effect definitions live in effects/. This file owns the canvas and playback lifecycle.
(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects.presets;
    let preset = 0, style = 0, intensity = 50, paused = false, disposed = false;
    let canvas = null, ctx = null, frame = null, lastTime = null;
    let width = 1, height = 1, elapsed = 0;
    let particles = [], sprite = null;
    const frameInterval = 1000 / 30;
    const enabled = () => !disposed && preset !== 0 && intensity > 0;

    function createParticles() {
        particles = effects[preset]?.createParticles(intensity, style) ?? [];
    }

    function resize() {
        if (!canvas) return;
        width = Math.max(1, window.innerWidth);
        height = Math.max(1, window.innerHeight);
        // Bound fill cost on high-DPI / 4K screens while keeping source rendering intact.
        const pixelRatio = Math.min(window.devicePixelRatio || 1, 1.5, 2560 / width, 1440 / height);
        canvas.width = Math.max(1, Math.round(width * pixelRatio));
        canvas.height = Math.max(1, Math.round(height * pixelRatio));
        ctx.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
        draw(0);
    }

    function ensureCanvas() {
        if (canvas) return true;
        if (!document.body) return false;
        const overlay = document.createElement('canvas');
        const context = overlay.getContext('2d');
        if (!context) return false;
        overlay.id = 'vp-atmosphere';
        overlay.setAttribute('aria-hidden', 'true');
        overlay.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;pointer-events:none;z-index:2147483647;';
        canvas = overlay;
        ctx = context;
        document.body.appendChild(canvas);
        sprite = effects[preset].createSprite?.(style) ?? null;
        resize();
        return true;
    }

    function stop() {
        if (frame !== null) cancelAnimationFrame(frame);
        frame = null;
        lastTime = null;
    }

    function releaseCanvas() {
        canvas?.remove();
        canvas = ctx = sprite = null;
    }

    function sync() {
        stop();
        if (!enabled()) {
            releaseCanvas();
            particles = [];
            return;
        }
        if (!ensureCanvas() || paused || document.hidden) return;
        frame = requestAnimationFrame(tick);
    }

    function tick(now) {
        frame = null;
        if (!enabled() || paused || document.hidden) return;
        if (lastTime === null) lastTime = now;
        const delta = now - lastTime;
        if (delta >= frameInterval) {
            // Resuming or a slow renderer must not advance particles across the screen.
            draw(Math.min(delta / 1000, 0.1));
            lastTime = now - delta % frameInterval;
        }
        frame = requestAnimationFrame(tick);
    }

    function draw(dt) {
        elapsed += dt;
        ctx.clearRect(0, 0, width, height);
        const scale = Math.min(width, height) / 1080;
        const opacity = 0.3 + intensity / 160;
        effects[preset].draw(ctx, particles, { dt, elapsed, width, height, scale, opacity, style }, sprite);
        ctx.globalAlpha = 1;
    }

    function onVisibilityChanged() { sync(); }
    function dispose() {
        disposed = true;
        stop();
        releaseCanvas();
        particles = [];
        window.removeEventListener('resize', resize);
        document.removeEventListener('visibilitychange', onVisibilityChanged);
        document.removeEventListener('DOMContentLoaded', sync);
        window.removeEventListener('pagehide', dispose);
    }

    window.vpAtmosphere = {
        setPreset(value) {
            if (disposed) return;
            const next = Number.isInteger(value) && effects[value] ? value : 0;
            if (preset === next) return;
            preset = next;
            releaseCanvas();
            createParticles();
            sync();
        },
        setStyle(value) {
            if (disposed) return;
            const next = Number.isInteger(value) && value >= 0 && value <= 1 ? value : 0;
            if (style === next) return;
            style = next;
            releaseCanvas();
            createParticles();
            sync();
        },
        setIntensity(value) {
            if (disposed || !Number.isFinite(value)) return;
            const next = Math.max(0, Math.min(100, value));
            if (intensity === next) return;
            intensity = next;
            createParticles();
            sync();
        },
        setPaused(value) {
            const next = Boolean(value);
            if (paused === next) return;
            paused = next;
            sync();
        },
        dispose,
    };
    window.addEventListener('resize', resize);
    window.addEventListener('pagehide', dispose);
    document.addEventListener('visibilitychange', onVisibilityChanged);
    document.addEventListener('DOMContentLoaded', sync);
})();
