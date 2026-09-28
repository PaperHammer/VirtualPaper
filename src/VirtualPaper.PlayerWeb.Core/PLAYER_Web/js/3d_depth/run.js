import * as THREE from './three.module.min.js';

/**
 * Library 3D 深度壁纸的播放器。
 * 输入是原图和对应的灰度深度图，片元着色器按鼠标位置偏移原图采样坐标，
 * 得到轻微的交互视差；这里不会生成新的深度图，也不会补全被遮挡的画面。
 * 页面宿主通过文件末尾挂到 window 的接口控制加载、填充方式和播放状态。
 */

// current 保存当前场景及其纹理、网格、渲染器；frame 是唯一待执行的动画帧 ID。
// 更换壁纸时先释放 current，因此任何时刻只允许一个场景占用 WebGL 资源。
let current = null, frame = null;
// 填充模式、鼠标视差开关和播放暂停相互独立；暂停不销毁已加载的场景。
let currentFitMode = 'fill', parallaxEnabled = false, paused = false;
// target 是归一化鼠标目标位置，mouse 是逐帧逼近它的实际位移位置。
const mouse = new THREE.Vector2(), target = new THREE.Vector2();

/**
 * 按需请求下一帧。frame 非空说明已有一帧排队，连续鼠标事件会合并到同一帧；
 * 纹理未就绪、暂停或页面不可见时不排队，避免空转占用 CPU/GPU。
 */
function requestRender() {
    if (frame === null && current?.ready && !paused && !document.hidden)
        frame = requestAnimationFrame(render);
}

function render() {
    // 回调已开始，先清空 ID，后续才可以安排下一帧。
    frame = null;
    if (!current?.ready || paused || document.hidden) return;
    // 用指数插值平滑鼠标动作；足够接近时直接对齐目标，终止帧循环。
    // 阈值比较的是二维距离的平方，和 distanceToSquared 的返回值一致。
    mouse.lerp(target, 0.18);
    const moving = mouse.distanceToSquared(target) > 0.000001;
    if (!moving) mouse.copy(target);
    current.renderer.render(current.scene, current.camera);
    if (moving) requestRender();
}

function disposeCurrent() {
    // 更换壁纸或离开页面时取消排队帧；先清空 current，让旧纹理加载回调失效。
    if (frame !== null) cancelAnimationFrame(frame);
    frame = null;
    if (!current) return;
    const old = current;
    current = null;
    // Three.js 的这些 GPU 对象不会因为 DOM 节点移除而自动释放。
    // 两张纹理、网格几何体、着色器材质和渲染器都要显式销毁。
    old.texture.dispose();
    old.depth.dispose();
    old.mesh.geometry.dispose();
    old.mesh.material.dispose();
    old.renderer.dispose();
    // 主动释放 WebGL 上下文，避免频繁切换壁纸时达到浏览器上下文上限。
    old.renderer.forceContextLoss();
    old.element.remove();
}

/**
 * 加载一组原图与深度图，并创建与它们配套的场景。
 * 两张纹理异步加载：每个回调都要检查 current === state，以防上一次加载
 * 晚于新壁纸完成后，把旧纹理或尺寸写入新场景。
 */
function resourceLoad3D(imgFilePath, depthFilePath) {
    disposeCurrent();
    mouse.set(0, 0);
    target.set(0, 0);
    // 3d_depth_map.html 初始已有占位 #content；重新加载时用新的容器替换。
    document.getElementById('content')?.remove();
    const element = document.createElement('div');
    element.id = 'content';
    element.className = 'source fade-in';
    element.setAttribute('draggable', 'false');
    document.querySelector('.root').appendChild(element);
    const scene = new THREE.Scene();
    // 图片网格位于 z=0，相机在 z=4；resizeContent 据此计算可见平面尺寸。
    const camera = new THREE.PerspectiveCamera(100, 1, 0.1, 1000);
    camera.position.set(0, 0, 4);
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setClearAlpha(0);
    renderer.setPixelRatio(window.devicePixelRatio);
    element.appendChild(renderer.domElement);
    // ready 只表示原图和深度图都可用于绘制；各纹理分别记录加载状态。
    // 图片实际宽高用于实现 fill/cover/contain/none/scale-down。
    const state = { scene, camera, renderer, element, ready: false,
        imageWidth: 1, imageHeight: 1, imageLoaded: false, depthLoaded: false };
    current = state;
    const loaded = () => {
        if (current !== state) return;
        // 加载任一纹理后都会重新计算尺寸；requestRender 会在 ready 前拒绝绘制。
        state.ready = state.imageLoaded && state.depthLoaded;
        resizeContent();
    };
    const loader = new THREE.TextureLoader();
    // 图片尺寸必须从加载后的纹理读取，而不能假设传入文件的尺寸与屏幕相同。
    // 原图失败时只记录错误，不把 imageLoaded 置为 true，因此不会绘制不完整场景。
    state.texture = loader.load(imgFilePath, tex => {
        if (current !== state) { tex.dispose(); return; }
        state.imageWidth = tex.image.naturalWidth || tex.image.width;
        state.imageHeight = tex.image.naturalHeight || tex.image.height;
        state.imageLoaded = true;
        loaded();
    }, undefined, error => console.error('Unable to load wallpaper texture', error));
    // 深度图是与原图逐像素对齐的灰度纹理；R 通道供片元着色器采样。
    state.depth = loader.load(depthFilePath, tex => {
        if (current !== state) { tex.dispose(); return; }
        state.depthLoaded = true;
        loaded();
    }, undefined, error => {
        if (current !== state) return;
        // 深度图失败时仍可显示原图：把视差强度归零，并提供有效的 1x1
        // 占位纹理，避免着色器采样缺失纹理。此状态也视为 depthLoaded。
        console.error('Unable to load depth texture; showing the original image', error);
        state.mesh.material.uniforms.uStrength.value = 0;
        state.depth.dispose();
        state.depth = new THREE.DataTexture(new Uint8Array([0, 0, 0, 255]), 1, 1);
        state.depth.needsUpdate = true;
        state.mesh.material.uniforms.uDepthTexture.value = state.depth;
        state.depthLoaded = true;
        loaded();
    });
    const material = new THREE.ShaderMaterial({
        // uMouse 是平滑后的 [-1, 1] 位移；uStrength 是 UV 坐标中的基础位移系数。
        // uAspect 根据网格的显示宽高修正横向位移，避免宽屏图被横向拉得过多。
        uniforms: {
            uTexture: { value: state.texture }, uDepthTexture: { value: state.depth },
            uMouse: { value: mouse }, uStrength: { value: 0.012 }, uAspect: { value: 1 },
        },
        vertexShader: `
            // Keep the mesh flat and pass UV coordinates to the fragment shader.
            varying vec2 vUv;
            void main() {
                vUv = uv;
                gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
            }
        `,
        fragmentShader: `
            uniform sampler2D uTexture;
            uniform sampler2D uDepthTexture;
            uniform vec2 uMouse;
            uniform float uStrength;
            uniform float uAspect;
            varying vec2 vUv;
            void main() {
                // Normalized Depth Anything V2 output: larger values are nearer.
                float depth = texture2D(uDepthTexture, vUv).r;
                // Smoothly fade the offset within 4% of the image border. Otherwise
                // sampling would reach outside the source image and stretch edge texels.
                vec2 border = min(vUv, 1.0 - vUv);
                float edge = smoothstep(0.0, 0.04, min(border.x, border.y));
                // Nearer pixels move more; correct horizontal motion for the mesh aspect.
                vec2 offset = uMouse * vec2(1.0 / uAspect, 1.0) * uStrength * depth * edge;
                // There are no reconstructed pixels behind an occluder. Compare depth
                // along the proposed sampling path and suppress motion across sharp jumps.
                vec2 candidate = clamp(vUv + offset, 0.0, 1.0);
                float otherDepth = texture2D(uDepthTexture, candidate).r;
                float middleDepth = texture2D(uDepthTexture, (vUv + candidate) * 0.5).r;
                float jump = max(abs(depth - otherDepth), abs(depth - middleDepth));
                offset *= 1.0 - smoothstep(0.04, 0.16, jump);
                // Final clamp keeps every color lookup inside the original texture.
                gl_FragColor = texture2D(uTexture, clamp(vUv + offset, 0.0, 1.0));
            }
        `,
    });
    state.mesh = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), material);
    scene.add(state.mesh);
    resizeContent();
    return 'init success';
}

/**
 * 按当前窗口尺寸和填充模式设置 canvas、相机及图片网格。
 * canvas 始终覆盖窗口；不同 object-fit 效果由网格缩放实现，而不是缩放 canvas。
 */
function resizeContent() {
    if (!current?.mesh) return;
    const { camera, renderer, mesh, imageWidth, imageHeight } = current;
    const width = Math.max(1, window.innerWidth), height = Math.max(1, window.innerHeight);
    renderer.setSize(width, height);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    // 图片平面在 z=0，相机在 z=4；由垂直视角计算该平面上可见的世界坐标高度。
    const visibleHeight = 2 * Math.tan(THREE.MathUtils.degToRad(camera.fov) / 2) * camera.position.z;
    let pixelWidth = width, pixelHeight = height;
    if (currentFitMode !== 'fill') {
        // fill 默认直接占满窗口；其他模式先在像素空间保持原图比例。
        // cover 取较大比例以填满窗口，contain 取较小比例以完整显示图片；
        // scale-down 还限制比例不超过 1，none 保持原始像素尺寸。
        let scale = 1;
        if (currentFitMode === 'cover') scale = Math.max(width / imageWidth, height / imageHeight);
        if (currentFitMode === 'contain') scale = Math.min(width / imageWidth, height / imageHeight);
        if (currentFitMode === 'scale-down') scale = Math.min(1, width / imageWidth, height / imageHeight);
        pixelWidth = imageWidth * scale;
        pixelHeight = imageHeight * scale;
    }
    // 像素尺寸换算成相机平面的世界坐标；显示宽高比也传给 shader 修正位移。
    mesh.scale.set(pixelWidth * visibleHeight / height, pixelHeight * visibleHeight / height, 1);
    mesh.material.uniforms.uAspect.value = pixelWidth / pixelHeight;
    requestRender();
}

function stopParallax() {
    // 关闭鼠标跟踪并清零视差，请求一帧把画面恢复到原始采样位置。
    parallaxEnabled = false;
    mouse.set(0, 0);
    target.set(0, 0);
    requestRender();
}

window.addEventListener('resize', resizeContent);
window.addEventListener('pagehide', disposeCurrent);
document.addEventListener('visibilitychange', () => {
    // 标签页隐藏后浏览器可能暂停 RAF；主动取消旧帧，可见时再按需绘制。
    if (document.hidden && frame !== null) {
        cancelAnimationFrame(frame);
        frame = null;
    } else requestRender();
});
document.addEventListener('mousemove', e => {
    if (!parallaxEnabled) return;
    // 鼠标坐标映射为 [-1, 1]；WebGL 的 Y 轴向上，因此要翻转屏幕 Y。
    // 这里只更新目标，实际平滑运动由 render 完成。
    target.set(Math.max(-1, Math.min(1, e.clientX / window.innerWidth * 2 - 1)),
        Math.max(-1, Math.min(1, 1 - e.clientY / window.innerHeight * 2)));
    requestRender();
});
document.addEventListener('mouseleave', () => { target.set(0, 0); requestRender(); });
// 宿主通过 window 调用这些入口。updateDimensions 直接重读 window 尺寸，
// updateFit3D 接收 base.js 转换后的填充模式；startParallax 只打开鼠标跟踪。
window.resourceLoad = resourceLoad3D;
window.updateDimensions = resizeContent;
window.startParallax = () => { parallaxEnabled = true; };
window.stopParallax = stopParallax;
window.updateFit3D = mode => { currentFitMode = mode; resizeContent(); return 'done'; };
window.play = () => { paused = false; requestRender(); };
window.pause = () => {
    // 暂停只停止后续绘制，保留纹理和 WebGL 场景以便快速恢复。
    paused = true;
    if (frame !== null) cancelAnimationFrame(frame);
    frame = null;
};
// 播放状态通知使用 isPaused=true 表示暂停，与 window.play 的语义相反。
window.playbackChanged = isPaused => isPaused ? window.pause() : window.play();
