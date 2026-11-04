(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;
    effects.presets[3] = {
        createParticles: intensity => effects.createParticles(Math.round(16 + intensity * 0.6)),
        createSprite: style => effects.createGlow(style === 1 ? '120,235,255' : '225,255,128'),
        draw(ctx, particles, view, sprite) {
            const { dt, elapsed, width, height, scale, opacity, style } = view;
            for (const p of particles) {
                effects.drift(p, dt, 0.003, -0.004);
                const rate = 0.28 + p.speed * 0.15;
                const pulse = Math.pow(0.5 + 0.5 * Math.sin(elapsed * (0.9 + p.speed * 0.55) + p.phase), 3);
                const radius = Math.max(1.5, (1.6 + p.depth * 2.8) * scale);
                const size = radius * 10;
                let x = 0, y = 0, alpha = 0;
                // Analytic trailing positions remain smooth after pause and resize.
                for (let trail = 3; trail >= 0; trail--) {
                    const lag = trail * 0.1;
                    const angle = (elapsed - lag) * rate + p.phase;
                    const nx = p.x - 0.05 - lag * 0.003 * p.speed
                        + Math.sin(angle) * 0.035 + Math.sin(angle * 1.9 + p.phase) * 0.009;
                    const ny = p.y - 0.05 + lag * 0.004 * p.speed + Math.cos(angle * 0.72 + p.phase) * 0.026;
                    x = nx * width; y = ny * height;
                    alpha = opacity * effects.edgeFade(nx, ny) * (0.32 + pulse * 0.68) * (0.65 + p.depth * 0.35);
                    ctx.globalAlpha = alpha * (trail === 0 ? 1 : 0.15 / trail);
                    ctx.drawImage(sprite, x - size / 2, y - size / 2, size, size);
                }
                ctx.globalAlpha = alpha;
                ctx.fillStyle = style === 1 ? '#d7fcff' : '#fff8be';
                ctx.beginPath();
                ctx.arc(x, y, Math.max(0.8, radius * 0.35), 0, Math.PI * 2);
                ctx.fill();
            }
        },
    };
})();
