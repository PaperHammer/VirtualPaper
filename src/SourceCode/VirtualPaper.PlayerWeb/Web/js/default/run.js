const scale = 1.05; // 缩放系数
let containerWidth = window.innerWidth;
let containerHeight = window.innerHeight;
let centerX = containerWidth / 2;
let centerY = containerHeight / 2;
let parallaxBackground;

let curVideoElementId = null; // 存储当前视频元素的ID
let volume = 0;
let speed = 0.0;
//let muted = false;

function init(width, height) {
    containerWidth = width;
    containerHeight = height;
    centerX = containerWidth / 2;
    centerY = containerHeight / 2;
    parallaxBackground = document.getElementById('content');

    return 'init success';
}

function resourceLoad(wallpaperType, filePath) {
    if (curVideoElementId) {
        videoRelease();
    }

    const element = document.querySelector('.root');
    const oldContent = document.getElementById('content');

    let newElement;
    switch (wallpaperType) {
        case 'RImage':
            newElement = `
                <div id="content" class="background fade-in" draggable="false">
                    <img draggable="false" class="source" src="${filePath}" alt=""/>
                </div>`;
            break;
        case 'RVideo':
            curVideoElementId = 'videoEle';
            newElement = `
                <div id="content" class="background fade-in" draggable="false">
                    <video draggable="false" id="videoEle" class="source" loop>
                        <source src="${filePath}" type="video/mp4">
                    </video>
                </div>`;
            break;
        default:
            return;
    }

    if (newElement) {
        // 动态插入新内容后立即移除旧内容，以减少闪烁
        element.insertAdjacentHTML('beforeend', newElement);
        //const newContent = document.getElementById('content');

        if (oldContent) {
            oldContent.setAttribute('class', 'fade-out');
            setTimeout(() => {
                oldContent.remove();
            }, 300);
        }
    }

    return "resourceLoad success";
}

function videoRelease() {
    curVideoElementId = null;
    var videoElement = document.getElementById('videoEle');
    if (videoElement) {
        videoElement.pause();
        videoElement.removeAttribute('src');
        videoElement = null;

        return "videoRelease success";
    }
}

//在某些浏览器（例如 Chrome 70.0）中，如果没有设置 muted 属性，autoplay 将不会生效。
function play() {
    var videoElement = document.getElementById('videoEle');
    if (videoElement) {
        videoElement.play();

        return "play success";
    }
}

function playbackChanged(isPaused) {
    var videoElement = document.getElementById('videoEle');
    if (videoElement) {
        if (isPaused) videoElement.pause();
        else videoElement.play();

        return "playbackChanged success";
    }
}

function audioMuteChanged(isMuted) {
    var videoElement = document.getElementById('videoEle');
    if (videoElement) {
        videoElement.muted = isMuted;

        return "audioMuteChanged success";
    }
}

function mouseMove(x, y) {
    // 计算鼠标与中心点的相对位置百分比
    const relX = (x - centerX) / centerX;
    const relY = (y - centerY) / centerY;

    const rotateX = relY * 2; // X轴旋转角度，根据Y轴偏移量调整
    const rotateY = relX * 2; // Y轴旋转角度，根据X轴偏移量调整

    parallaxBackground.style.transform = `scale(${scale}) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
}

function mouseOut() {
    parallaxBackground.style.transform = 'scale(1) rotateX(0deg) rotateY(0deg)';
}