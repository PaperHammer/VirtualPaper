// Dependency-free browser/host integration regression tests.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../../src/VirtualPaper.PlayerWeb.Core/PLAYER_Web');

function browser() {
    const frames = new Map(), listeners = new Map(), canvases = [];
    let nextFrame = 0;
    function events(target) {
        return {
            addEventListener(name, handler) { listeners.set(`${target}:${name}`, handler); },
            removeEventListener(name) { listeners.delete(`${target}:${name}`); },
        };
    }
    const calls = [];
    function finiteCoordinates(...values) {
        values.forEach(value => assert.ok(Number.isFinite(value), 'drawing coordinates must stay finite'));
    }
    const context2d = {
        setTransform(...args) { this.transform = args; },
        clearRect() { calls.push('clear'); },
        createRadialGradient: () => ({ addColorStop() {} }),
        createLinearGradient: () => ({ addColorStop() {} }),
        fillRect() {}, beginPath() {}, closePath() {}, save() {}, restore() {},
        translate: finiteCoordinates, rotate: finiteCoordinates, scale: finiteCoordinates,
        moveTo: finiteCoordinates, lineTo: finiteCoordinates, bezierCurveTo: finiteCoordinates,
        arc(...args) { finiteCoordinates(...args); calls.push('arc'); }, fill() {}, stroke() { calls.push('stroke'); },
        drawImage(sprite, ...args) { finiteCoordinates(...args); calls.push('image'); },
    };
    Object.defineProperty(context2d, 'globalAlpha', {
        set(value) { assert.ok(Number.isFinite(value) && value >= 0 && value <= 1, 'opacity must stay in range'); },
    });
    const body = { children: [], appendChild(node) { this.children.push(node); } };
    const document = {
        ...events('document'), body, hidden: false,
        createElement(name) {
            assert.equal(name, 'canvas', 'do not replace source content');
            const canvas = {
                style: {}, setAttribute() {}, getContext: () => context2d,
                remove() { body.children = body.children.filter(node => node !== this); },
            };
            canvases.push(canvas);
            return canvas;
        },
        querySelector() { throw Error('overlay must not touch source DOM'); },
    };
    const window = { ...events('window'), innerWidth: 1920, innerHeight: 1080, devicePixelRatio: 1 };
    const sandbox = vm.createContext({ window, document, console,
        requestAnimationFrame(handler) { frames.set(++nextFrame, handler); return nextFrame; },
        cancelAnimationFrame(id) { frames.delete(id); },
    });
    const html = fs.readFileSync(path.join(root, 'default.html'), 'utf8');
    const scripts = [...html.matchAll(/<script src="(js\/(?:effects\/[^\"]+|atmosphere)\.js)"><\/script>/g)];
    for (const [, script] of scripts) {
        vm.runInContext(fs.readFileSync(path.join(root, script), 'utf8'), sandbox);
    }
    return { window, document, body, frames, canvases, calls, context2d, listeners, sandbox,
        emit(target, name) { listeners.get(`${target}:${name}`)?.(); },
        tick(now) { const pending = [...frames.values()]; frames.clear(); pending.forEach(handler => handler(now)); },
    };
}

const b = browser(), api = b.window.vpAtmosphere;
assert.equal(b.canvases.length, 0);
assert.equal(b.frames.size, 0, 'default disabled costs no frames/canvas');
api.setIntensity(70);
api.setStyle(1);
api.setStyle(0);
assert.equal(b.canvases.length, 0);
api.setPreset(1);
assert.equal(b.body.children.length, 1);
assert.match(b.body.children[0].style.cssText, /pointer-events:none/);
assert.equal(b.frames.size, 1);
b.tick(0); b.tick(34);
assert.ok(b.calls.includes('image'));
const scheduledFrame = [...b.frames.keys()][0];
api.setIntensity(70);
api.setPaused(false);
assert.equal([...b.frames.keys()][0], scheduledFrame, 'unchanged settings must not restart playback');
api.setPaused(true);
api.setStyle(1);
assert.equal(b.frames.size, 0, 'changing style preserves host pause');
api.setStyle(0);
assert.equal(b.frames.size, 0);
b.document.hidden = true; b.emit('document', 'visibilitychange');
b.document.hidden = false; b.emit('document', 'visibilitychange');
assert.equal(b.frames.size, 0, 'visibility cannot override host pause');
api.setPaused(false);
assert.equal(b.frames.size, 1);
b.document.hidden = true; b.emit('document', 'visibilitychange');
assert.equal(b.frames.size, 0);
api.setPaused(false);
assert.equal(b.frames.size, 0, 'host resume cannot override hidden document');
b.document.hidden = false; b.emit('document', 'visibilitychange');
b.tick(100000);
const previous = b.calls.length;
b.tick(100010);
assert.equal(b.calls.length, previous, 'throttle drawing to 30fps and reset time on resume');
b.window.innerWidth = 2560; b.window.innerHeight = 1440; b.window.devicePixelRatio = 2;
b.emit('window', 'resize');
assert.equal(b.body.children[0].width, 2560);
assert.equal(b.body.children[0].height, 1440);
assert.equal(b.context2d.transform[0], 1, 'bound high-DPI backing buffer');
api.setIntensity(0);
assert.equal(b.frames.size, 0);
assert.equal(b.body.children.length, 0, 'zero intensity releases canvas');
api.setIntensity(100);
for (const preset of [1, 2, 3, 4, 5, 6, 7]) {
  for (const style of [0, 1]) {
    api.setStyle(style);
    api.setPreset(preset);
    b.calls.length = 0;
    b.tick(0); b.tick(34); b.tick(100);
    const drawing = preset === 2 && style === 0 ? 'arc' : preset === 4 || preset === 5 ? 'stroke' : 'image';
    assert.ok(b.calls.includes(drawing), `preset ${preset}, style ${style} must draw`);
    assert.equal(b.body.children.length, 1, 'switching presets replaces, never stacks canvases');
    assert.equal(b.frames.size, 1);
    assert.ok(b.calls.length < 1200, 'particle count and soft sprite passes are bounded');
  }
}
api.setPreset(999);
assert.equal(b.body.children.length, 0);
api.setPreset(1);
b.calls.length = 0;
for (let i = 0; i < 60; i++) b.tick(i * 1000 / 60);
assert.ok(b.calls.filter(call => call === 'clear').length <= 30, '60Hz callbacks must not exceed 30 draws/sec');
api.setPreset(2); b.emit('window', 'pagehide');
assert.equal(b.frames.size, 0);
assert.equal(b.body.children.length, 0);
assert.equal(b.listeners.size, 0);
api.setPreset(1); api.setIntensity(60); api.setPaused(false);
api.setStyle(1);
assert.equal(b.frames.size, 0, 'disposed page cannot restart');

// Run through respawns and wind changes in landscape and portrait viewports.
const longRun = browser();
longRun.window.vpAtmosphere.setIntensity(100);
for (const [width, height] of [[1920, 1080], [704, 960]]) {
    Object.assign(longRun.window, { innerWidth: width, innerHeight: height });
    longRun.emit('window', 'resize');
    for (const preset of [1, 2, 3, 4, 5, 6, 7]) {
      for (const style of [0, 1]) {
        longRun.window.vpAtmosphere.setStyle(style);
        longRun.window.vpAtmosphere.setPreset(preset);
        for (let step = 0; step < 480; step++) {
            longRun.tick(step * 100);
            longRun.calls.length = 0;
        }
      }
    }
}
longRun.window.vpAtmosphere.dispose();

// Exercise the actual numeric property dispatcher and default playback hooks.
const host = browser();
vm.runInContext(fs.readFileSync(path.join(root, 'js/base.js'), 'utf8'), host.sandbox);
vm.runInContext(fs.readFileSync(path.join(root, 'js/default/run.js'), 'utf8'), host.sandbox);
vm.runInContext("propertyListener('Atmosphere', '2'); propertyListener('AtmosphereIntensity', '75'); propertyListener('AtmosphereStyle', '1');", host.sandbox);
host.document.getElementById = () => null;
vm.runInContext('playbackChanged(true)', host.sandbox);
assert.equal(host.frames.size, 0, 'non-video wallpapers pause overlay too');
vm.runInContext('play()', host.sandbox);
assert.equal(host.frames.size, 1);
for (const html of ['default.html', '3d_depth_map.html']) {
    const source = fs.readFileSync(path.join(root, html), 'utf8');
    const scripts = [...source.matchAll(/<script src="(js\/(?:effects\/[^\"]+|atmosphere)\.js)"><\/script>/g)]
        .map(match => match[1]);
    assert.deepEqual(scripts, ['js/effects/common.js', 'js/effects/stardust.js', 'js/effects/snow.js',
        'js/effects/fireflies.js', 'js/effects/meteor.js', 'js/effects/rain.js', 'js/effects/petals.js',
        'js/effects/bokeh.js', 'js/atmosphere.js'], `${html}: load helpers and effects before the player`);
}
console.log('Atmosphere: defaults, presets, config dispatch, pause/visibility, resize/DPI, frame limit and disposal passed.');
