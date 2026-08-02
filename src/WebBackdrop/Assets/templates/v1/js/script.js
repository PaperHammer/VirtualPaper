const canvas = document.getElementById('wallpaper');
const ctx = canvas.getContext('2d');
const timeText = document.getElementById('time');

const messages = {
    'zh-CN': {
        'template.title': '交互光流',
        'template.hint': '移动鼠标或点击画面生成光流'
    },
    'en-US': {
        'template.title': 'Interactive Glow',
        'template.hint': 'Move the mouse or click to create glowing streams'
    }
};

function getLanguage() {
    return navigator.language === 'zh-CN' || navigator.language === 'zh' ? 'zh-CN' : 'en-US';
}

function applyI18n() {
    const texts = messages[getLanguage()];
    document.documentElement.lang = getLanguage();

    document.querySelectorAll('[data-i18n]').forEach(element => {
        const key = element.dataset.i18n;
        if (!key || !texts[key]) return;

        if (element.tagName === 'TITLE') {
            element.textContent = texts[key];
        }
        else {
            element.textContent = texts[key];
        }
    });
}

applyI18n();

const pointer = {
    x: 0,
    y: 0,
    px: 0,
    py: 0,
    active: false
};

const particles = [];
const palette = ['#38bdf8', '#818cf8', '#c084fc', '#f0abfc', '#22d3ee'];
let width = 0;
let height = 0;
let lastAutoSpawn = 0;

function resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    width = window.innerWidth;
    height = window.innerHeight;
    canvas.width = Math.floor(width * dpr);
    canvas.height = Math.floor(height * dpr);
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

function spawn(x, y, amount = 8, force = 1) {
    for (let i = 0; i < amount; i++) {
        const angle = Math.random() * Math.PI * 2;
        const speed = (1.2 + Math.random() * 4) * force;
        particles.push({
            x,
            y,
            vx: Math.cos(angle) * speed,
            vy: Math.sin(angle) * speed,
            radius: 2 + Math.random() * 7,
            life: 1,
            decay: 0.008 + Math.random() * 0.016,
            color: palette[Math.floor(Math.random() * palette.length)]
        });
    }
}

function movePointer(x, y) {
    pointer.px = pointer.x;
    pointer.py = pointer.y;
    pointer.x = x;
    pointer.y = y;

    const dx = pointer.x - pointer.px;
    const dy = pointer.y - pointer.py;
    const distance = Math.hypot(dx, dy);
    if (pointer.active && distance > 4) {
        spawn(pointer.x, pointer.y, Math.min(18, Math.ceil(distance / 8)), 0.7);
    }
}

function drawBackground(time) {
    const gradient = ctx.createRadialGradient(
        width * 0.5 + Math.cos(time * 0.0002) * width * 0.2,
        height * 0.45 + Math.sin(time * 0.00025) * height * 0.2,
        0,
        width * 0.5,
        height * 0.5,
        Math.max(width, height) * 0.72
    );

    gradient.addColorStop(0, 'rgba(56, 189, 248, 0.20)');
    gradient.addColorStop(0.42, 'rgba(30, 41, 59, 0.34)');
    gradient.addColorStop(1, 'rgba(2, 6, 23, 0.72)');

    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, width, height);
}

function drawParticles() {
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';

    for (let i = particles.length - 1; i >= 0; i--) {
        const p = particles[i];
        p.x += p.vx;
        p.y += p.vy;
        p.vx *= 0.985;
        p.vy *= 0.985;
        p.life -= p.decay;

        const radius = p.radius * (0.35 + p.life);
        const glow = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, radius * 7);
        glow.addColorStop(0, `${p.color}ee`);
        glow.addColorStop(0.35, `${p.color}66`);
        glow.addColorStop(1, `${p.color}00`);

        ctx.globalAlpha = Math.max(p.life, 0);
        ctx.fillStyle = glow;
        ctx.beginPath();
        ctx.arc(p.x, p.y, radius * 7, 0, Math.PI * 2);
        ctx.fill();

        if (p.life <= 0) {
            particles.splice(i, 1);
        }
    }

    ctx.restore();
    ctx.globalAlpha = 1;
}

function render(time) {
    ctx.clearRect(0, 0, width, height);
    drawBackground(time);
    drawParticles();

    if (time - lastAutoSpawn > 1800) {
        lastAutoSpawn = time;
        spawn(Math.random() * width, Math.random() * height, 5, 0.55);
    }

    requestAnimationFrame(render);
}

function updateTime() {
    timeText.textContent = new Date().toLocaleTimeString('zh-CN', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
    });
}

window.addEventListener('resize', resize);
window.addEventListener('pointermove', event => movePointer(event.clientX, event.clientY));
window.addEventListener('pointerdown', event => {
    pointer.active = true;
    movePointer(event.clientX, event.clientY);
    spawn(event.clientX, event.clientY, 36, 1.2);
});
window.addEventListener('pointerup', () => pointer.active = false);
window.addEventListener('pointerleave', () => pointer.active = false);

resize();
updateTime();
setInterval(updateTime, 1000);
spawn(window.innerWidth * 0.5, window.innerHeight * 0.5, 48, 0.9);
requestAnimationFrame(render);
