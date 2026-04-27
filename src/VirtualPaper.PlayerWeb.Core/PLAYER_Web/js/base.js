const templateFilter = "saturate(s#) hue-rotate(d#deg) brightness(b#) contrast(c#)";

let saturate = 0.0;
let hueRotate = 0;
let brightness = 0.0;
let contrast = 0.0
let newFit = "";
let curVideoElementId = null; // 存储当前视频元素的ID

// TimePerception 叠加的偏移量（不覆盖用户设置）
let tpBrightnessOffset = 0.0;
let tpHueOffset = 0;
let tpSaturateOffset = 0.0;

let tpIntervalId = null;
let tpConfig = null; // 存储 C# 下发的参数

function propertyListener(propertyType, val) {
    switch (propertyType) {
        case "Volume":
            volume = parseFloat(val);
            break;
        case "Speed":
            speed = parseFloat(val);
            break;
        case "Saturation":
            saturate = parseFloat(val);
            break;
        case "Hue":
            hueRotate = parseInt(val);
            break;
        case "Brightness":
            brightness = parseFloat(val);
            break;
        case "Contrast":
            contrast = parseFloat(val);
            break;
        case "Scaling":
            objectFitChanged(parseInt(val));
            break;
        case "TimePerception":
            runTimePerception(val);
            break;
    }

    return applyFilter();
}

function applyFilter() {
    const element = document.querySelector('.source');
    const contentDiv = document.getElementById('content');

    if (contentDiv && element) {
        const finalSaturate = clamp(saturate + tpSaturateOffset, 0.0, 3.0);
        const finalBrightness = clamp(brightness + tpBrightnessOffset, 0.1, 2.0);
        const finalHueRotate = ((hueRotate + clamp(tpHueOffset, -30, 30)) % 360 + 360) % 360;
        const finalContrast = contrast;

        let filter = templateFilter;
        filter = filter.replace(/s#/g, finalSaturate.toFixed(3));
        filter = filter.replace(/d#/g, Math.round(finalHueRotate));
        filter = filter.replace(/b#/g, finalBrightness.toFixed(3));
        filter = filter.replace(/c#/g, finalContrast);

        contentDiv.style.filter = filter.trim();
        element.style.objectFit = newFit;
    }

    if (element && curVideoElementId) {
        element.volume = volume;
        element.playbackRate = speed;
        element.muted = muted;
    }

    return "applyFilter success";
}

function clamp(val, min, max) {
    return Math.min(Math.max(val, min), max);
}

function objectFitChanged(value) {
    switch (value) {
        case 0:
            newFit = 'fill';
            break;
        case 1:
            newFit = 'contain';
            break;
        case 2:
            newFit = 'cover';
            break;
        case 3:
            newFit = 'none'
            break;
        case 4:
            newFit = 'scale-down';
            break;
        default:
            newFit = 'fill';
            break;
    }

    if (window.updateFit3D) {
        window.updateFit3D(newFit);
    }
}

/**
 * 由 C# 调用，传入 JSON 字符串
 * 示例：
 * {
 *   "enabled": true,
 *   "sunrise": 1745123400000,   // ms timestamp
 *   "sunset":  1745168400000,
 *   "phases": {
 *     "night":  { "brightness": -0.3, "hue": 220, "saturate": -0.2 },
 *     "dawn":   { "brightness":  0.1, "hue":  30, "saturate":  0.3 },
 *     "day":    { "brightness":  0.0, "hue":   0, "saturate":  0.0 },
 *     "dusk":   { "brightness": -0.1, "hue":  20, "saturate":  0.2 }
 *   },
 *   "transitionMinutes": 30
 * }
 */
function runTimePerception(val) {
    // 停止旧循环
    if (tpIntervalId !== null) {
        clearInterval(tpIntervalId);
        tpIntervalId = null;
    }

    // 解析参数
    try {
        tpConfig = typeof val === 'string' ? JSON.parse(val) : val;
    } catch (e) {
        console.error('TimePerception: invalid config', e);
        return;
    }

    if (!tpConfig || !tpConfig.enabled) {
        // 关闭：清零偏移量并重新渲染
        tpBrightnessOffset = 0;
        tpHueOffset = 0;
        tpSaturateOffset = 0;
        applyFilter();
        return;
    }

    // 立即执行一次，然后每分钟更新
    tickTimePerception();
    tpIntervalId = setInterval(tickTimePerception, 60 * 1000);
}

/**
 * 把 "HH:mm" 解析成指定时间戳当天对应的本地时间戳（ms）
 * @param {string} hhmm  - "HH:mm" 格式
 * @param {number} baseMs - 基准时间戳（默认 Date.now()，模拟时自动跟随）
 */
function todayTimeFromHHMM(hhmm, baseMs = Date.now()) {
    const [h, m] = hhmm.split(':').map(Number);
    const d = new Date(baseMs);   // ← 用 baseMs 构造，而不是 new Date()
    d.setHours(h, m, 0, 0);
    return d.getTime();
}

function tickTimePerception() {
    if (!tpConfig) return;

    const now = Date.now(); // 本地时间戳，和下面构造的时间戳单位一致
    const { sunrise, sunset, phases, transitionMinutes } = tpConfig;
    const transitionMs = (transitionMinutes ?? 30) * 60 * 1000;

    // 每次 tick 都重新构造今天的日出日落时间戳
    const sunriseMs = todayTimeFromHHMM(sunrise, now);
    const sunsetMs = todayTimeFromHHMM(sunset, now);

    const target = calcPhaseTarget(now, sunriseMs, sunsetMs, phases, transitionMs);

    tpBrightnessOffset = target.brightness;
    tpHueOffset = target.hue;
    tpSaturateOffset = target.saturate;

    applyFilter();
}

/**
 * 根据当前时间计算目标偏移量（含过渡插值）
 */
function calcPhaseTarget(now, sunrise, sunset, phases, transitionMs) {
    // 定义四个关键时刻
    const dawnStart = sunrise - transitionMs;
    const dawnEnd = sunrise + transitionMs;
    const duskStart = sunset - transitionMs;
    const duskEnd = sunset + transitionMs;

    // 防止过渡区域重叠（transitionMinutes 设置过大时）
    const safeDawnEnd = Math.min(dawnEnd, (dawnStart + duskEnd) / 2);
    const safeDuskStart = Math.max(duskStart, (dawnStart + duskEnd) / 2);

    // 纯夜晚
    if (now < dawnStart || now >= duskEnd) {
        return phases.night;
    }
    // dawn 过渡：night → day
    if (now >= dawnStart && now < safeDawnEnd) {
        const t = (now - dawnStart) / (safeDawnEnd - dawnStart);
        return lerpPhase(phases.night, phases.day, easeInOut(t));
    }
    // 纯白天
    if (now >= safeDawnEnd && now < safeDuskStart) {
        return phases.day;
    }
    // dusk 过渡：day → night
    if (now >= safeDuskStart && now < duskEnd) {
        const t = (now - safeDuskStart) / (duskEnd - safeDuskStart);
        return lerpPhase(phases.day, phases.night, easeInOut(t));
    }

    return phases.night; // 兜底改为 night 更安全（走到这里说明时间异常）
}

function lerpPhase(a, b, t) {
    return {
        brightness: lerp(a.brightness, b.brightness, t),
        hue: lerp(a.hue, b.hue, t),
        saturate: lerp(a.saturate, b.saturate, t)
    };
}

function lerp(a, b, t) {
    return a + (b - a) * t;
}

function easeInOut(t) {
    return t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
}
