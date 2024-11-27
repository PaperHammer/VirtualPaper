const templateFilter = "saturate(s#) hue-rotate(d#deg) brightness(b#) contrast(c#)";

let saturate = 0.0;
let hueRotate = 0;
let brightness = 0.0;
let contrast = 0.0
let newFit = "";

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
            hueRotate = parseInt(val)
            break;
        case "Brightness":
            brightness = parseFloat(val);
            break;
        case "Contrast":
            contrast = parseFloat(val)
            break;
        case "Scaling":
            objectFitChanged(val);
            break;
    }

    return applyFilter();
}

function applyFilter() {
    const element = document.querySelector('.source');
    const contentDiv = document.getElementById('content');

    if (contentDiv && element) {
        var filter = templateFilter;
        filter = filter.replace(new RegExp('s#', 'g'), saturate);
        filter = filter.replace(new RegExp('d#', 'g'), hueRotate);
        filter = filter.replace(new RegExp('b#', 'g'), brightness);
        filter = filter.replace(new RegExp('c#', 'g'), contrast);

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
}
