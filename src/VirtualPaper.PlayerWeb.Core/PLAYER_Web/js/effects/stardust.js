(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;
    effects.presets[1] = {
        createParticles: (intensity, style) => effects.createParticles(Math.round(18 + intensity * (style === 1 ? 0.9 : 1.6))),
        createSprite: style => ({
            cool: effects.createGlow('210,230,255'),
            warm: effects.createGlow('255,227,172'),
            star: style === 1 ? effects.createSprite(ctx => {
                ctx.fillStyle = '#fff3d6';
                ctx.beginPath();
                ctx.moveTo(32, 3); ctx.lineTo(37, 27); ctx.lineTo(61, 32); ctx.lineTo(37, 37);
                ctx.lineTo(32, 61); ctx.lineTo(27, 37); ctx.lineTo(3, 32); ctx.lineTo(27, 27);
                ctx.closePath(); ctx.fill();
            }) : null,
        }),
        draw(ctx, particles, view, sprites) {
            const { dt, elapsed, width, height, scale, opacity, style } = view;
            for (const p of particles) {
                effects.drift(p, dt, 0.003 + p.depth * 0.003, -0.004 - p.depth * 0.008);
                const nx = p.x - 0.05 + Math.sin(elapsed * 0.35 + p.phase) * 0.009;
                const ny = p.y - 0.05;
                const x = nx * width, y = ny * height;
                const pulse = 0.5 + 0.5 * Math.sin(elapsed * (0.6 + p.speed * 0.55) + p.phase);
                const fade = effects.edgeFade(nx, ny);
                const size = Math.max(style === 1 ? 1.5 : 0.65, p.size * (0.55 + p.depth * 1.15) * scale) * 6;
                ctx.globalAlpha = opacity * fade * (0.2 + p.depth * 0.6) * (0.35 + pulse * 0.65);
                ctx.drawImage(p.phase > 5.2 ? sprites.warm : sprites.cool, x - size / 2, y - size / 2, size, size);
                if (style === 1) {
                    ctx.drawImage(sprites.star, x - size / 2, y - size / 2, size, size);
                }
                // Only a few nearby stars form a short glint at their brightest point.
                if (style === 0 && p.depth > 0.82 && pulse > 0.85) {
                    const arm = size * (0.18 + pulse * 0.12);
                    ctx.globalAlpha = opacity * fade * Math.pow(pulse, 8) * 0.65;
                    ctx.strokeStyle = '#edf5ff';
                    ctx.lineWidth = Math.max(0.6, scale * 0.75);
                    ctx.beginPath();
                    ctx.moveTo(x - arm, y); ctx.lineTo(x + arm, y);
                    ctx.moveTo(x, y - arm); ctx.lineTo(x, y + arm);
                    ctx.stroke();
                }
            }
        },
    };
})();
