// ===== 模拟配置 =====
const SIM_STEP_MS = 60 * 1000;
const TICK_INTERVAL_MS = 50; // 现实每50ms = 模拟1分钟
const SIM_DURATION_DAYS = 2;

// ===== Mock Date.now =====
let simNow = new Date();
simNow.setHours(0, 0, 0, 0);
let simTimeMs = simNow.getTime();

const _realDateNow = Date.now;
Date.now = () => simTimeMs;

// ===== 注入配置 =====
const config = {
    enabled: true,
    sunrise: "06:23",
    sunset: "19:00",
    transitionMinutes: 30,
    phases: {
        night: { brightness: -0.3, hue: 220, saturate: -0.2 },
        dawn: { brightness: 0.1, hue: 30, saturate: 0.3 },
        day: { brightness: 0.0, hue: 0, saturate: 0.0 },
        dusk: { brightness: -0.1, hue: 20, saturate: 0.2 }
    }
};
tpConfig = config;

// ===== 模拟循环 =====
const totalSteps = SIM_DURATION_DAYS * 24 * 60;
let step = 0;

function simStep() {
    if (step >= totalSteps) {
        Date.now = _realDateNow;
        console.log("===== 模拟结束 =====");
        return;
    }

    simTimeMs += SIM_STEP_MS;
    step++;

    tickTimePerception();

    const d = new Date(simTimeMs);
    const day = Math.floor(step / (24 * 60)) + 1;
    const hh = d.getHours().toString().padStart(2, '0');
    const mm = d.getMinutes().toString().padStart(2, '0');

    // 每整点打印一次，减少刷屏；其余时间只在相位变化时打印
    if (mm === '00') {
        console.log(
            `[Day${day} ${hh}:${mm}]`,
            `brightness=${tpBrightnessOffset.toFixed(3)}`,
            `hue=${tpHueOffset.toFixed(1).padStart(6)}`,
            `saturate=${tpSaturateOffset.toFixed(3)}`
        );
    }

    setTimeout(simStep, TICK_INTERVAL_MS);
}

console.log("===== 开始模拟（每50ms = 模拟1分钟）=====");
simStep();