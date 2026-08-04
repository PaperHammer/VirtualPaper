window.VirtualPaper = {
    /**
     * Hot-reload a CSS file by updating its <link> href with a cache-busting
     * query parameter.  Matches by filename (last segment of path).
     */
    reloadCss(path) {
        var filename = path.split('/').pop().split('\\').pop();
        var links = document.querySelectorAll("link[rel='stylesheet']");

        for (var i = 0; i < links.length; i++) {
            var link = links[i];
            if (link.href.indexOf(filename) !== -1) {
                var q = link.href.indexOf('?');
                var base = q >= 0 ? link.href.substring(0, q) : link.href;
                link.href = base + '?v=' + Date.now();
                console.log('[VirtualPaper HMR] CSS reloaded: ' + filename);
                return;
            }
        }

        console.warn('[VirtualPaper HMR] CSS link not found: ' + filename);
    },

    /**
     * Hot-reload a JS file by removing the old <script> tag and creating
     * a new one, which re-executes the script.  Matches by filename.
     */
    reloadJs(path) {
        var filename = path.split('/').pop().split('\\').pop();
        var scripts = document.querySelectorAll('script[src]');

        for (var i = 0; i < scripts.length; i++) {
            var s = scripts[i];
            if (s.src.indexOf(filename) !== -1) {
                var old = s;
                var script = document.createElement('script');
                var q = old.src.indexOf('?');
                var base = q >= 0 ? old.src.substring(0, q) : old.src;
                script.src = base + '?v=' + Date.now();

                // Copy non-src attributes so behaviour is preserved
                for (var j = 0; j < old.attributes.length; j++) {
                    var attr = old.attributes[j];
                    if (attr.name !== 'src') {
                        script.setAttribute(attr.name, attr.value);
                    }
                }

                old.parentNode.replaceChild(script, old);
                console.log('[VirtualPaper HMR] JS reloaded: ' + filename);
                return;
            }
        }

        console.warn('[VirtualPaper HMR] JS script not found: ' + filename);
    },

    /**
     * Generic resource-change callback (images, JSON, etc.).
     * Extend this for new resource types.
     */
    reloadResource(path) {
        console.log('[VirtualPaper HMR] resource changed: ' + path);
    }
};

// Listen for HMR commands sent via postMessage from the shell page.
// The shell cannot access the iframe's DOM directly (cross-origin), so it
// uses postMessage to signal which file changed.
window.addEventListener('message', function (event) {
    var msg = event.data;
    if (!msg || msg.source !== 'virtualpaper-hmr') return;

    switch (msg.type) {
        case 'reloadCss':
            window.VirtualPaper.reloadCss(msg.path);
            break;
        case 'reloadJs':
            window.VirtualPaper.reloadJs(msg.path);
            break;
        case 'reloadResource':
            window.VirtualPaper.reloadResource(msg.path);
            break;
        case 'reloadPage':
            location.reload();
            break;
        default:
            console.warn('[VirtualPaper HMR] unknown message type: ' + msg.type);
    }
});
