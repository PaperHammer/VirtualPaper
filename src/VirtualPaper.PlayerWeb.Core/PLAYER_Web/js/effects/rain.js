(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;
    effects.presets[5] = {
        createParticles: (intensity, style) => effects.createParticles(Math.round(48 + intensity * (style === 1 ? 2.5 : 1.9))),
        draw(ctx, particles, { dt, elapsed, width, height, scale, opacity, style }) {
            const slant = (style === 1 ? 0.32 : 0.12) + Math.sin(elapsed * 0.35) * 0.05;
            ctx.strokeStyle = '#d5e8ff'; ctx.lineCap = 'round';
            for (const p of particles) {
                const velocity = (0.45 + p.depth * 0.7) * (style === 1 ? 1.5 : 1);
                effects.drift(p, dt, velocity * slant * height / width, velocity);
                const x = (p.x - 0.05) * width, y = (p.y - 0.05) * height;
                const length = Math.max(6, ((style === 1 ? 25 : 8) + p.depth * 25) * scale);
                ctx.globalAlpha = opacity * (0.18 + p.depth * 0.5);
                ctx.lineWidth = Math.max(0.6, (0.5 + p.depth) * scale);
                ctx.beginPath(); ctx.moveTo(x - slant * length, y - length); ctx.lineTo(x, y); ctx.stroke();
            }
        },
    };
})();
