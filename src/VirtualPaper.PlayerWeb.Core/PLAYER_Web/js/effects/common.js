// Shared particle helpers and preset definitions. Preset IDs match AtmosphereEffect.
// Load this file first, then effect scripts, then atmosphere.js.
// Each preset defines createParticles(intensity, style), optional createSprite(style),
// and draw(ctx, particles, { dt, elapsed, width, height, scale, opacity, style }, sprite).
(() => {
    'use strict';
    const random = (min, max) => min + Math.random() * (max - min);

    function createParticles(count) {
        return Array.from({ length: count }, () => ({
            x: Math.random(), y: Math.random(), size: random(0.7, 2.3),
            speed: random(0.6, 1.4), phase: random(0, Math.PI * 2),
            depth: Math.random(),
        }));
    }

    function createSprite(draw) {
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = 64;
        draw(canvas.getContext('2d'));
        return canvas;
    }

    function createGlow(color) {
        // Cache the soft sprite instead of computing blur/shadows for every particle.
        return createSprite(sprite => {
            const gradient = sprite.createRadialGradient(32, 32, 0, 32, 32, 32);
            gradient.addColorStop(0, `rgba(${color},1)`);
            gradient.addColorStop(0.2, `rgba(${color},0.7)`);
            gradient.addColorStop(1, `rgba(${color},0)`);
            sprite.fillStyle = gradient;
            sprite.fillRect(0, 0, 64, 64);
        });
    }

    function drift(particle, dt, horizontal, vertical) {
        particle.x += dt * horizontal * particle.speed;
        particle.y += dt * vertical * particle.speed;
        particle.x = (particle.x + 1.1) % 1.1;
        particle.y = (particle.y + 1.1) % 1.1;
    }

    window.vpAtmosphereEffects = {
        presets: {},
        random, createParticles, createSprite, createGlow, drift,
        // Smooth entry/exit prevents a visible jump when a wandering particle wraps.
        edgeFade: (x, y) => Math.max(0, Math.min(1, Math.min(x + 0.02, 1.02 - x, y + 0.02, 1.02 - y) * 20)),
    };
})();
