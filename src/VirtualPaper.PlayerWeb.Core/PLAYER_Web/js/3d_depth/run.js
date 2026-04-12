import * as THREE from './three.module.min.js';

// --- 全局变量 ---
let width = window.innerWidth;
let height = window.innerHeight;
let mouse = new THREE.Vector2();

let camera, scene, renderer;
let planeMesh; 
let imgAspect = 1; 
let currentFitMode = 'fill'; 

function resourceLoad3D(imgFilePath, depthFilePath) {
    // DOM 清理逻辑
    const root = document.querySelector('.root');
    const oldContent = document.getElementById('content');
    if (oldContent) {
        oldContent.setAttribute('class', 'fade-out');
        setTimeout(() => { oldContent.remove(); }, 500);
    }

    const newContent = document.createElement('div');
    newContent.id = 'content';
    newContent.className = 'source fade-in';
    newContent.setAttribute('draggable', 'false');
    root.appendChild(newContent);

    // 初始化 Scene
    scene = new THREE.Scene();
    scene.background = null;

    // 初始化 Camera
    const fov = 100;
    const aspect = width / height;
    const near = 0.1;
    const far = 1000;
    camera = new THREE.PerspectiveCamera(fov, aspect, near, far);
    camera.position.set(0, 0, 4); // 相机位置 z=4

    // 加载纹理并处理宽高比 (异步修正)
    const textureLoader = new THREE.TextureLoader();
    
    // 加载主图，加载完成后更新比例并重新计算布局
    const texture = textureLoader.load(imgFilePath, (tex) => {
        imgAspect = tex.image.width / tex.image.height;
        resizeContent(); // 图片加载完了，重新根据比例缩放一次
    });
    
    const dapthTexture = textureLoader.load(depthFilePath);

    // 创建 Mesh
    const geometry = new THREE.PlaneGeometry(1, 1); // 基础尺寸 1x1

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
                // 简单的视差计算
                float x = vUv.x + (uMouse.x * 0.02 * depthValue); // 稍微调小一点系数防止撕裂
                float y = vUv.y + (uMouse.y * 0.02 * depthValue);
                gl_FragColor = texture2D(uTexture, vec2(x, y));
            }
        `,
    });

    planeMesh = new THREE.Mesh(geometry, material);
    scene.add(planeMesh);

    // 初始化 Renderer (注意：去掉 const)
    renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
    });
    renderer.setClearAlpha(0);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(width, height); // 使用全局 width/height

    // 挂载 Canvas
    newContent.appendChild(renderer.domElement);

    // 启动动画循环 (只保留一个)
    function animate() {
        requestAnimationFrame(animate);
        // 更新鼠标 Uniform
        if(planeMesh && planeMesh.material) {
             planeMesh.material.uniforms.uMouse.value = mouse;
        }
        renderer.render(scene, camera);
    }
    animate();

    // 立即调用一次 resize 确保初始状态正确
    resizeContent();

    return 'init success';
}

/**
 * 计算 Object-Fit 对应的 Mesh Scale
 */
function resizeContent() {
    // 关键检查：确保全局变量已初始化
    if (!camera || !planeMesh || !renderer) {
        console.warn('ThreeJS resources not ready yet.');
        return;
    }

    const screenWidth = window.innerWidth;
    const screenHeight = window.innerHeight;
    const screenAspect = screenWidth / screenHeight;

    // 更新渲染尺寸
    renderer.setSize(screenWidth, screenHeight);
    camera.aspect = screenAspect;
    camera.updateProjectionMatrix();

    // 计算相机在 z=0 平面(Mesh所在位置)的可见高度
    // distance = camera.z (4) - mesh.z (0) = 4
    const distance = camera.position.z - planeMesh.position.z; 
    const vFov = THREE.MathUtils.degToRad(camera.fov); // 转弧度
    const visibleHeight = 2 * Math.tan(vFov / 2) * distance;
    const visibleWidth = visibleHeight * screenAspect;

    let scaleX, scaleY;

    switch (currentFitMode) {
        case 'cover':
            if (screenAspect > imgAspect) {
                // 屏幕更宽：宽度对齐，高度溢出
                scaleX = visibleWidth;
                scaleY = visibleWidth / imgAspect;
            } else {
                // 屏幕更高：高度对齐，宽度溢出
                scaleY = visibleHeight;
                scaleX = visibleHeight * imgAspect;
            }
            break;

        case 'contain':
        case 'scale-down':
            if (screenAspect > imgAspect) {
                // 屏幕更宽：高度对齐，两侧留白
                scaleY = visibleHeight;
                scaleX = visibleHeight * imgAspect;
            } else {
                // 屏幕更高：宽度对齐，上下留白
                scaleX = visibleWidth;
                scaleY = visibleWidth / imgAspect;
            }
            break;
            
        case 'none':
            // 原始比例，假设高度占满屏幕的 80% 或者 1:1 映射
            // 这里暂且按"高度填满"处理，或者你可以定一个固定单位值
            scaleY = visibleHeight; 
            scaleX = visibleHeight * imgAspect;
            break;

        case 'fill':
        default:
            // 强制拉伸
            scaleX = visibleWidth;
            scaleY = visibleHeight;
            break;
    }

    // 应用缩放
    planeMesh.scale.set(scaleX, scaleY, 1);
}

function updateFit3D(mode) {
    console.log('Update Fit Mode:', mode);
    currentFitMode = mode;
    resizeContent();
    return 'done';
}

/* 监听窗口变化 */
window.addEventListener('resize', () => {
    width = window.innerWidth;
    height = window.innerHeight;
    resizeContent();
});

function mouseMove3D(x, y) {
    // 归一化鼠标坐标 (-1 到 1)
    mouse.x = (x / window.innerWidth) * 2 - 1;
    mouse.y = -(y / window.innerHeight) * 2 + 1; // WebGL Y轴通常向上，DOM Y轴向下，建议反转
}

function mouseOut3D() {
    mouse.x = 0;
    mouse.y = 0;
}

function updateDimensions3D(w, h) {
    width = w;
    height = h;
}

window.resourceLoad = resourceLoad3D;
window.updateDimensions = updateDimensions3D;
window.mouseMove = mouseMove3D;
window.mouseOut = mouseOut3D;
window.updateFit3D = updateFit3D;