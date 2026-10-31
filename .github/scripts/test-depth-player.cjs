// Dependency-free lifecycle/layout regression tests; GPU shader quality needs visual testing.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const callbacks = new Map(), events = {}, loads = [], renderers = [];
let nextFrame = 0;
class Vector2 {
    constructor() { this.set(0, 0); }
    set(x, y) { this.x = x; this.y = y; return this; }
    lerp(v, a) { this.x += (v.x - this.x) * a; this.y += (v.y - this.y) * a; }
    copy(v) { this.set(v.x, v.y); }
    distanceToSquared(v) { return (v.x - this.x) ** 2 + (v.y - this.y) ** 2; }
}
class Disposable { dispose() { this.disposed = true; } }
class Texture extends Disposable { constructor() { super(); this.image = { width: 200, height: 100 }; } }
class Renderer extends Disposable {
    constructor() { super(); this.domElement = {}; this.renders = 0; renderers.push(this); }
    setClearAlpha() {} setPixelRatio() {} setSize() {} forceContextLoss() {}
    render() { this.renders++; }
}
const context = vm.createContext({
    console, Uint8Array,
    window: { innerWidth: 1000, innerHeight: 800, devicePixelRatio: 1,
        addEventListener: (k, f) => events[k] = f },
    document: { hidden: false, getElementById: () => null,
        querySelector: () => ({ appendChild() {} }),
        createElement: () => ({ setAttribute() {}, appendChild() {}, remove() {} }),
        addEventListener: (k, f) => events[k] = f },
    requestAnimationFrame: f => { callbacks.set(++nextFrame, f); return nextFrame; },
    cancelAnimationFrame: id => callbacks.delete(id),
    THREE: {
        Vector2, WebGLRenderer: Renderer, DataTexture: Texture,
        Scene: class { add() {} },
        PerspectiveCamera: class { constructor(fov) { this.fov = fov; this.position = { set(x, y, z) { this.z = z; } }; } updateProjectionMatrix() {} },
        TextureLoader: class { load(url, done, progress, fail) { const tex = new Texture(); loads.push({ tex, done, fail }); return tex; } },
        ShaderMaterial: class extends Disposable { constructor(options) { super(); Object.assign(this, options); } },
        PlaneGeometry: Disposable,
        Mesh: class { constructor(geometry, material) { Object.assign(this, { geometry, material, scale: { set(x, y) { this.x = x; this.y = y; } } }); } },
        MathUtils: { degToRad: x => x * Math.PI / 180 },
    },
});
const code = fs.readFileSync(path.join(__dirname, '../../src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/3d_depth/run.js'), 'utf8');
vm.runInContext(code.replace(/^import .*;\r?\n/, ''), context);
const evaluate = expression => vm.runInContext(expression, context);
let now = 0;
function tick(ms = 1000 / 60) {
    now += ms;
    const pending = [...callbacks.values()]; callbacks.clear(); pending.forEach(f => f(now));
}
function drain() {
    let count = 0;
    while (callbacks.size) {
        assert.ok(++count < 100, 'render loop must settle');
        tick();
    }
}
function finishLoad() { loads.slice(-2).forEach(item => item.done(item.tex)); drain(); }
context.window.resourceLoad('a', 'depth');
assert.equal(callbacks.size, 0, 'wait for both textures');
finishLoad();
assert.equal(renderers[0].renders, 1, 'idle image renders once');
assert.equal(evaluate('current.mesh.material.uniforms.uStrength.value'), 0.024, 'default to standard strength');
context.window.startParallax();
events.mousemove({ clientX: 950, clientY: 50 });
events.mousemove({ clientX: 950, clientY: 50 });
assert.equal(callbacks.size, 1, 'coalesce events');
drain();
context.window.stopParallax(); drain();
assert.equal(evaluate('mouse.x'), 0);
context.window.updateFit3D('none'); drain();
const nativeScale = evaluate('current.mesh.scale.x');
context.window.updateFit3D('scale-down'); drain();
assert.equal(evaluate('current.mesh.scale.x'), nativeScale, 'small images do not upscale');
context.window.updateFit3D('contain'); drain();
assert.ok(evaluate('current.mesh.scale.x') > nativeScale);
context.window.resourceLoad('b', 'depth');
const stale = loads.slice(-2);
context.window.resourceLoad('c', 'depth');
assert.ok(renderers[0].disposed && renderers[1].disposed);
stale.forEach(item => item.done(item.tex));
assert.equal(evaluate('current.ready'), false, 'late callbacks cannot mutate replacement');
finishLoad();
const atmospherePauses = [];
context.window.vpAtmosphere = { setPaused: value => atmospherePauses.push(value) };
context.window.playbackChanged(true);
context.window.updateFit3D('cover');
assert.equal(callbacks.size, 0, 'paused layout changes do not render');
context.window.playbackChanged(false); drain();
assert.deepEqual(atmospherePauses, [true, false], 'depth player forwards playback to the overlay');
// Exercise the shared dispatcher used by both the preview and desktop player.
vm.runInContext(fs.readFileSync(path.join(__dirname, '../../src/VirtualPaper.PlayerWeb.Core/PLAYER_Web/js/base.js'), 'utf8'), context);
for (const [value, expected] of [[0, 0.016], [2, 0.036], [1, 0.024], [99, 0.024]]) {
    evaluate(`propertyListener('ParallaxStrength', '${value}')`); drain();
    assert.equal(evaluate('current.mesh.material.uniforms.uStrength.value'), expected);
}
evaluate("propertyListener('ParallaxAutoDrift', true)");
const beforeDrift = renderers.at(-1).renders;
for (let i = 0; i < 120; i++) tick();
assert.equal(callbacks.size, 1, 'drift runs without enabling mouse parallax');
assert.ok(evaluate('Math.abs(mouse.x) + Math.abs(mouse.y)') > 0.05, 'auto drift visibly advances');
const driftDraws = renderers.at(-1).renders - beforeDrift;
assert.ok(driftDraws >= 55 && driftDraws <= 61, 'auto drift stays near 30fps despite timestamp rounding');
context.window.startParallax();
events.mousemove({ clientX: 1000, clientY: 0 });
for (let i = 0; i < 60; i++) tick();
assert.ok(evaluate('Math.abs(mouse.x) <= 1 && Math.abs(mouse.y) <= 1'), 'combined displacement stays bounded');
const beforeLeave = evaluate('mouse.x');
context.window.stopParallax();
assert.equal(evaluate('mouse.x'), beforeLeave, 'leaving mouse tracking does not snap active drift');
tick();
const phaseBeforePause = evaluate('driftTime');
context.window.pause();
assert.equal(callbacks.size, 0, 'pause cancels auto drift');
tick(60000);
context.window.play(); tick();
assert.equal(evaluate('driftTime'), phaseBeforePause, 'resume excludes paused time');
context.document.hidden = true; events.visibilitychange();
assert.equal(callbacks.size, 0, 'hidden page cancels auto drift');
tick(60000);
context.document.hidden = false; events.visibilitychange(); tick();
assert.equal(evaluate('driftTime'), phaseBeforePause, 'hidden time does not advance drift');
evaluate("propertyListener('ParallaxAutoDrift', 'false')"); drain();
assert.equal(evaluate('mouse.x'), 0, 'disabling drift settles back to the original image');
const idleRenders = renderers.at(-1).renders; tick(1000);
assert.equal(renderers.at(-1).renders, idleRenders, 'disabled drift restores idle rendering');

// A missing depth image remains a static fallback, even after changing strength.
evaluate("propertyListener('ParallaxAutoDrift', true)");
context.window.resourceLoad('fallback', 'missing-depth');
loads.at(-2).done(loads.at(-2).tex);
const originalError = console.error;
try { console.error = () => {}; loads.at(-1).fail(new Error('missing depth')); }
finally { console.error = originalError; }
drain();
evaluate("propertyListener('ParallaxStrength', '2')"); drain();
assert.equal(evaluate('current.mesh.material.uniforms.uStrength.value'), 0);
assert.equal(callbacks.size, 0, 'fallback does not run an invisible drift loop');
context.window.resourceLoad('reloaded', 'depth');
loads.slice(-2).forEach(item => item.done(item.tex)); tick();
assert.equal(evaluate('current.mesh.material.uniforms.uStrength.value'), 0.036, 'new images retain chosen strength');
assert.equal(callbacks.size, 1, 'new images resume enabled auto drift');
events.pagehide();
assert.equal(callbacks.size, 0);
assert.ok(renderers.every(r => r.disposed));
console.log('Depth player: lifecycle, fit modes, strength, auto drift, idle/pause/visibility and depth fallback passed.');
