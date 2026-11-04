(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;
    effects.presets[2] = {
        createParticles: (intensity, style) => effects.createParticles(Math.round(24 + intensity * (style === 1 ? 0.85 : 1.65))),
        createSprite: style => style === 0 ? effects.createGlow('235,245,255') : effects.createSprite(ctx => {
            ctx.strokeStyle = '#f3f9ff'; ctx.lineWidth = 2.4; ctx.lineCap = 'round';
            ctx.translate(32, 32);
            ctx.beginPath();
            for (let arm = 0; arm < 6; arm++) {
                const angle = arm * Math.PI / 3, dx = Math.cos(angle), dy = Math.sin(angle);
                ctx.moveTo(0, 0); ctx.lineTo(dx * 26, dy * 26);
                for (const side of [-1, 1]) {
                    const branch = angle + side * 0.8;
                    ctx.moveTo(dx * 15, dy * 15);
                    ctx.lineTo(dx * 15 - Math.cos(branch) * 9, dy * 15 - Math.sin(branch) * 9);
                }
            }
            ctx.stroke();
        }),
        draw(ctx, particles, view, sprite) {
            const { dt, elapsed, width, height, scale, opacity, style } = view;
            const wind = 0.01 + Math.sin(elapsed * 0.23) * 0.018 + Math.sin(elapsed * 0.61) * 0.007;
            for (const p of particles) {
                effects.drift(p, dt, wind * (0.35 + p.depth * 0.65), 0.02 + p.depth * p.depth * 0.095);
                const sway = Math.sin(elapsed * (0.55 + p.speed * 0.3) + p.phase);
                const x = (p.x - 0.05 + sway * (0.002 + p.depth * 0.005)) * width;
                const y = (p.y - 0.05) * height;
                const radius = Math.max(0.55, (0.6 + p.depth * p.depth * 3.4) * scale);
                const alpha = opacity * (0.24 + p.depth * 0.62);
                if (style === 1) {
                    const size = Math.max(7, (10 + p.depth * 18) * scale);
                    ctx.globalAlpha = opacity * (0.45 + p.depth * 0.5);
                    ctx.save(); ctx.translate(x, y); ctx.rotate(p.phase + elapsed * (0.15 + p.depth * 0.45));
                    ctx.drawImage(sprite, -size / 2, -size / 2, size, size);
                    ctx.restore();
                    continue;
                }
                // The closest flakes are softer; distant flakes stay small and faint.
                if (p.depth > 0.75) {
                    const size = radius * 5;
                    ctx.globalAlpha = alpha * 0.3;
                    ctx.drawImage(sprite, x - size / 2, y - size / 2, size, size);
                }
                ctx.globalAlpha = alpha;
                ctx.fillStyle = '#f2f7ff';
                ctx.beginPath();
                ctx.arc(x, y, radius, 0, Math.PI * 2);
                ctx.fill();
            }
        },
    };
})();
