import * as THREE from './three.module.min.js';

let width;
let height;
let mouse;

function resourceLoad3D(imgFilePath, depthFilePath) {
    // 先处理旧 content：加 fade-out，动画结束后移除
    const root = document.querySelector('.root');
    const oldContent = document.getElementById('content');
    if (oldContent) {
        // 替换 class 为 fade-out，避免叠加 class 影响布局
        oldContent.setAttribute('class', 'fade-out');
        setTimeout(() => {
            oldContent.remove();
        }, 500); // 与 CSS 动画时长 0.5s 对齐
    }

    // 创建新的 content 容器，添加 fade-in
    const newContent = document.createElement('div');
    newContent.id = 'content';
    // 新 content 最终需要有 source 的样式，这里先加上 source + fade-in
    newContent.className = 'source fade-in';
    newContent.setAttribute('draggable', 'false');
    root.appendChild(newContent);

    /* 背景 / 场景 */
    const scene = new THREE.Scene();
    scene.background = null;

    /* 相机 */
    const fov = 100; // 视野范围
    const aspect = width / height; // 画布的宽高比
    const near = 0.1; // 近平面
    const far = 1000; // 远平面
    const camera = new THREE.PerspectiveCamera(fov, aspect, near, far);
    camera.position.set(0, 0, 4);

    /* 加载纹理 */
    const textureLoader = new THREE.TextureLoader();
    const texture = textureLoader.load(imgFilePath);      // 图片
    const dapthTexture = textureLoader.load(depthFilePath); // 深度图

    /* 鼠标坐标 2D 向量 */
    mouse = new THREE.Vector2();

    /* 创建平面 */
    const geometry = new THREE.PlaneGeometry(16, 10);

    /* 着色器材质 */
    const material = new THREE.ShaderMaterial({
        uniforms: {
            uTexture: { value: texture },
            uDepthTexture: { value: dapthTexture },
            uMouse: { value: mouse },
        },
        vertexShader: `
            varying vec2 vUv;
            void main() {
                vUv = uv;
                gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0);
            }
        `,
        fragmentShader: `
            uniform sampler2D uTexture;
            uniform sampler2D uDepthTexture;
            uniform vec2 uMouse;
            varying vec2 vUv;
            void main() {
                vec4 color = texture2D(uTexture, vUv);
                vec4 depth = texture2D(uDepthTexture, vUv);
                float depthValue = depth.r;
                float x = vUv.x + uMouse.x * 0.01 * depthValue;
                float y = vUv.y + uMouse.y * 0.01 * depthValue;
                vec4 newColor = texture2D(uTexture, vec2(x, y));
                gl_FragColor = newColor;
            }
        `,
    });

    const plane = new THREE.Mesh(geometry, material);
    scene.add(plane);

    /* 渲染器 */
    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
    });
    renderer.setClearAlpha(0);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(window.innerWidth, window.innerHeight);

    requestAnimationFrame(function animate() {
        material.uniforms.uMouse.value = mouse;
        requestAnimationFrame(animate);
        renderer.render(scene, camera);
    });

    // 把 canvas 挂到新的 content 上
    newContent.appendChild(renderer.domElement);

    /* 监听窗口变化 */
    window.addEventListener('resize', () => {
        camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();

        renderer.setSize(window.innerWidth, window.innerHeight);
        renderer.setPixelRatio(window.devicePixelRatio);
    });

    return 'init success';
}

function mouseMove3D(x, y) {
    if (mouse) {
        mouse.x = (x / width) * 2 - 1;
        mouse.y = (y / height) * 2 - 1;
    }
}

function mouseOut3D() {
    if (mouse) {
        mouse.x = 0;
        mouse.y = 0;
    }
}

function updateDimensions3D(w, h) {
    width = w;
    height = h;
}

window.resourceLoad = resourceLoad3D;
window.updateDimensions = updateDimensions3D;
window.mouseMove = mouseMove3D;
window.mouseOut = mouseOut3D;