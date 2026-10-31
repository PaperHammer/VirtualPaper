(() => {
    'use strict';
    const effects = window.vpAtmosphereEffects;

    function respawn(particle, style) {
        particle.x = effects.random(0.02, 0.4);
        particle.y = effects.random(0.02, 0.27);
        particle.angle = effects.random(0.55, 0.85);
        particle.velocity = style === 1 ? effects.random(0.35, 0.55) : effects.random(0.5, 0.85);
        particle.life = style === 1 ? effects.random(2, 2.6) : effects.random(1.3, 1.9);
        particle.tail = effects.random(0.2, style === 1 ? 0.45 : 0.34);
    }

    effects.presets[4] = {
        createParticles(intensity, style) {
            const particles = effects.createParticles(2 + Math.floor(intensity / (style === 1 ? 60 : 40)));
            particles.forEach((p, i) => {
                respawn(p, style);
                p.age = i === 0 ? 0.18 : -i * 0.65;
            });
            return particles;
        },
        createSprite: style => effects.createGlow(style === 1 ? '255,212,170' : '210,235,255'),
        draw(ctx, particles, view, sprite) {
            const { dt, width, height, scale, opacity, style } = view;
            const span = Math.min(width, height);
            for (const p of particles) {
                p.age += dt;
                if (p.age > p.life) {
                    respawn(p, style);
                    p.age = -effects.random(0.5, 1.6);
                }
                if (p.age < 0) continue;
                const travel = p.age * p.velocity * span;
                const dx = Math.cos(p.angle), dy = Math.sin(p.angle);
                const x = p.x * width + dx * travel;
                const y = p.y * height + dy * travel;
                const length = Math.min(p.tail * span, travel);
                const tailX = dx * length, tailY = dy * length;
                const alpha = opacity * Math.min(1, p.age / 0.12) * Math.min(1, (p.life - p.age) / 0.4);
                const tail = ctx.createLinearGradient(x - tailX, y - tailY, x, y);
                tail.addColorStop(0, style === 1 ? 'rgba(195,145,255,0)' : 'rgba(135,190,255,0)');
                tail.addColorStop(0.65, style === 1 ? 'rgba(255,165,180,0.65)' : 'rgba(175,215,255,0.55)');
                tail.addColorStop(1, style === 1 ? 'rgba(255,245,210,1)' : 'rgba(245,250,255,1)');
                ctx.strokeStyle = tail;
                const lineWidth = Math.max(1.5, 2.6 * scale) * (style === 1 ? 1.5 : 1);
                ctx.lineCap = 'round';
                ctx.beginPath();
                ctx.moveTo(x - tailX, y - tailY);
                ctx.lineTo(x, y);
                ctx.globalAlpha = alpha * 0.2;
                ctx.lineWidth = lineWidth * 4;
                ctx.stroke();
                ctx.globalAlpha = alpha;
                ctx.lineWidth = lineWidth;
                ctx.stroke();
                const size = lineWidth * 14;
                ctx.drawImage(sprite, x - size / 2, y - size / 2, size, size);
                ctx.fillStyle = '#ffffff';
                ctx.beginPath();
                ctx.arc(x, y, lineWidth * 0.85, 0, Math.PI * 2);
                ctx.fill();
            }
        },
    };
})();
