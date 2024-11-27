import * as THREE from './three.module.min.js';

function init3D(imgFilePath, depthFilePath) {
    /*背景/场景*/
    const scene = new THREE.Scene();
    scene.background = null;

    /*相机*/
    const fov = 100 // 视野范围
    const aspect = 2; // 相机默认值 = 2, 画布的宽高比
    //const aspect = window.innerWidth / window.innerHeight; // 相机默认值 = 2, 画布的宽高比
    const near = 0.1 // 近平面
    const far = 1000 // 远平面
    const camera = new THREE.PerspectiveCamera(fov, aspect, near, far);
    camera.position.set(0, 0, 4);

    /*加载纹理*/
    const textureLoader = new THREE.TextureLoader();
    const texture = textureLoader.load(imgFilePath);//加载图片本身
    const dapthTexture = textureLoader.load(depthFilePath);//加载深度图

    /*鼠标坐标2维向量*/
    const mouse = new THREE.Vector2();

    /*创建平面*/
    const geometry = new THREE.PlaneGeometry(16, 10);

    /*着色器材质*/
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
            uniform  sampler2D uDepthTexture;
            uniform vec2  uMouse;
            varying vec2 vUv;
            void main() {
                vec4 color = texture2D(uTexture, vUv);
                vec4 depth = texture2D(uDepthTexture, vUv);
                float depthValue = depth.r;
                float x = vUv.x + uMouse.x*0.01*depthValue;
                float y = vUv.y + uMouse.y*0.01*depthValue;
                vec4 newColor = texture2D(uTexture,vec2(x,y));
                gl_FragColor = newColor;
            }
            `,
    });

    const plane = new THREE.Mesh(geometry, material);
    scene.add(plane);

    /*渲染器*/
    const renderer = new THREE.WebGLRenderer({
        antialias: true, // 反走样 抗锯齿
        alpha: true, // 启用透明度
    });
    renderer.setClearAlpha(0);
    renderer.setPixelRatio(window.devicePixelRatio); // 使用设备分辨率
    renderer.setSize(window.innerWidth, window.innerHeight);
    requestAnimationFrame(function animate() {
        material.uniforms.uMouse.value = mouse;
        requestAnimationFrame(animate);
        renderer.render(scene, camera);
    });

    const container = document.getElementById('content');
    container.appendChild(renderer.domElement); // 向指定 div 中添加


    /*监听鼠标计算位移量*/
    window.addEventListener('mousemove', (event) => {
        mouse.x = (event.clientX / window.innerWidth) * 2 - 1;
        mouse.y = (event.clientY / window.innerHeight) * 2 - 1;
    });

    /*监听窗口变化*/
    window.addEventListener('resize', () => {
        //camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();

        renderer.setSize(window.innerWidth, window.innerHeight);
        renderer.setPixelRatio(window.devicePixelRatio);
    });

    return 'init success';
}

window.init = init3D;