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
function drain() {
    let count = 0;
    while (callbacks.size) {
        assert.ok(++count < 100, 'render loop must settle');
        const pending = [...callbacks.values()]; callbacks.clear(); pending.forEach(f => f());
    }
}
function finishLoad() { loads.slice(-2).forEach(item => item.done(item.tex)); drain(); }
context.window.resourceLoad('a', 'depth');
assert.equal(callbacks.size, 0, 'wait for both textures');
finishLoad();
assert.equal(renderers[0].renders, 1, 'idle image renders once');
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
context.window.playbackChanged(true);
context.window.updateFit3D('cover');
assert.equal(callbacks.size, 0, 'paused layout changes do not render');
context.window.playbackChanged(false); drain();
events.pagehide();
assert.equal(callbacks.size, 0);
assert.ok(renderers.every(r => r.disposed));
console.log('Depth player: lifecycle, idle rendering, cancellation of frames, stale loads and fit modes passed.');
