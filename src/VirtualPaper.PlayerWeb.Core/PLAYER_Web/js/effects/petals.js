(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;
    effects.presets[6] = {
        createParticles: intensity => effects.createParticles(Math.round(20 + intensity * 0.7)),
        createSprite: style => effects.createSprite(ctx => {
            const color = ctx.createLinearGradient(12, 8, 48, 54);
            color.addColorStop(0, style === 1 ? '#ffd1dc' : '#fff0f7');
            color.addColorStop(1, style === 1 ? '#dc4f77' : '#f5a1c6');
            ctx.fillStyle = color;
            ctx.beginPath(); ctx.moveTo(32, 56);
            if (style === 1) {
                ctx.bezierCurveTo(1, 45, 4, 11, 28, 10);
                ctx.bezierCurveTo(58, 4, 62, 43, 32, 56);
            } else {
                ctx.bezierCurveTo(0, 34, 12, 4, 29, 14);
                ctx.lineTo(32, 20); ctx.lineTo(35, 14);
                ctx.bezierCurveTo(57, 4, 62, 34, 32, 56);
            }
            ctx.fill();
        }),
        draw(ctx, particles, { dt, elapsed, width, height, scale, opacity }, sprite) {
            const wind = 0.016 + Math.sin(elapsed * 0.24) * 0.006;
            for (const p of particles) {
                effects.drift(p, dt, wind, 0.018 + p.depth * 0.04);
                const x = (p.x - 0.05 + Math.sin(elapsed * 0.8 + p.phase) * 0.012) * width;
                const y = (p.y - 0.05) * height;
                const size = Math.max(7, (8 + p.depth * 16) * scale);
                ctx.globalAlpha = opacity * (0.55 + p.depth * 0.4);
                ctx.save(); ctx.translate(x, y); ctx.rotate(p.phase + elapsed * (0.3 + p.speed * 0.6));
                ctx.scale(0.35 + (0.5 + 0.5 * Math.sin(elapsed * 0.9 + p.phase)) * 0.65, 1);
                ctx.drawImage(sprite, -size / 2, -size / 2, size, size); ctx.restore();
            }
        },
    };
})();
