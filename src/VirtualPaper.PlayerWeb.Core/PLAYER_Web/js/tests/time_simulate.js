// ─── 时间加速测试工具 ───────────────────────────────────────────────
// speed:     加速倍率（1440 = 1天/分钟，60 = 1小时/分钟）
// startHour: 模拟起始小时（0~23，默认从当前时间开始）
(function setupFakeTime(speed = 120, startHour = 4) {
    const msPerDay = 24 * 60 * 60 * 1000;

    // 构造今天 startHour 点的时间戳
    const today = new Date();
    today.setHours(startHour, 0, 0, 0);
    const fakeStart = today.getTime();
    const realStart = performance.now();

    const _realDateNow = Date.now.bind(Date);

    Date.now = function () {
        const elapsed = performance.now() - realStart; // 真实流逝 ms
        const fakeElapsed = elapsed * speed;           // 加速后流逝 ms
        return fakeStart + fakeElapsed;
    };

    console.log(
        `%c⏩ 时间加速已启动`,
        'color: #4CAF50; font-weight: bold',
        `\n起始: ${new Date(Date.now()).toLocaleTimeString()}`,
        `\n速率: ${speed}x（每秒 = 现实 ${speed} 秒）`
    );

    // 每秒打印当前模拟时间，方便观察
    const timer = setInterval(() => {
        console.log(`🕐 模拟时间: ${new Date(Date.now()).toLocaleTimeString()}`);
    }, 1000);

    // 返回停止函数
    window._stopFakeTime = function () {
        clearInterval(timer);
        Date.now = _realDateNow;
        console.log('%c⏹ 时间加速已停止', 'color: #f44336; font-weight: bold');
    };
})(120, 4); // 120x 速率，从凌晨 4 点开始（覆盖日出过渡期）
