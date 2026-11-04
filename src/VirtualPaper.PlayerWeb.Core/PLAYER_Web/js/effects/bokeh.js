(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;
    effects.presets[7] = {
        createParticles: (intensity, style) => effects.createParticles(Math.round(8 + intensity * (style === 1 ? 0.4 : 0.32))),
        createSprite: style => ['178,212,255', '255,199,222', '255,230,171'].map(color => style === 0
            ? effects.createGlow(color)
            : effects.createSprite(ctx => {
                const gradient = ctx.createRadialGradient(23, 21, 2, 32, 32, 28);
                gradient.addColorStop(0, 'rgba(255,255,255,0.03)');
                gradient.addColorStop(0.8, `rgba(${color},0.03)`);
                gradient.addColorStop(0.94, `rgba(${color},0.55)`);
                gradient.addColorStop(1, `rgba(${color},0)`);
                ctx.fillStyle = gradient; ctx.fillRect(0, 0, 64, 64);
                ctx.strokeStyle = 'rgba(255,255,255,0.65)'; ctx.lineWidth = 1.8;
                ctx.beginPath(); ctx.arc(32, 32, 23, Math.PI * 1.1, Math.PI * 1.55); ctx.stroke();
            })),
        draw(ctx, particles, { dt, elapsed, width, height, scale, opacity, style }, sprites) {
            for (const p of particles) {
                effects.drift(p, dt, 0.002, style === 1 ? -0.018 : -0.005);
                const nx = p.x - 0.05 + Math.sin(elapsed * 0.25 + p.phase) * 0.012, ny = p.y - 0.05;
                const size = Math.max(18, (25 + p.depth * 100) * scale);
                const pulse = 0.65 + Math.sin(elapsed * 0.4 + p.phase) * 0.35;
                ctx.globalAlpha = opacity * effects.edgeFade(nx, ny) * pulse * (style === 1 ? 0.6 : 0.3);
                const sprite = sprites[Math.floor(p.phase / (Math.PI * 2) * sprites.length)];
                ctx.drawImage(sprite, nx * width - size / 2, ny * height - size / 2, size, size);
            }
        },
    };
})();
