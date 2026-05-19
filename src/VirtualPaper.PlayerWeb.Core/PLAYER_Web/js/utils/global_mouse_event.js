(function () {

    // ============================================
    // 全局虚拟鼠标状态
    // ============================================

    const virtualMouse = {

        // 原始像素坐标
        x: 0,
        y: 0,

        // 0~1
        normalizedX: 0,
        normalizedY: 0,

        // WebGL 坐标
        // (-1 ~ 1)
        glX: 0,
        glY: 0,

        buttons: 0,

        currentTarget: null,
        lastTarget: null
    };

    // 全局暴露（可选）
    window.VirtualMouse = virtualMouse;

    // ============================================
    // 工具
    // ============================================

    function normalize(x, y) {

        virtualMouse.normalizedX =
            x / window.innerWidth;

        virtualMouse.normalizedY =
            y / window.innerHeight;

        virtualMouse.glX =
            virtualMouse.normalizedX * 2 - 1;

        virtualMouse.glY =
            -(virtualMouse.normalizedY * 2 - 1);
    }

    function createMouseEvent(type, x, y, buttons) {

        return new MouseEvent(type, {

            bubbles: true,
            cancelable: true,
            composed: true,

            clientX: x,
            clientY: y,

            screenX: x,
            screenY: y,

            buttons: buttons
        });
    }

    function createPointerEvent(type, x, y, buttons) {

        return new PointerEvent(type, {

            bubbles: true,
            cancelable: true,
            composed: true,

            clientX: x,
            clientY: y,

            pointerId: 1,
            pointerType: 'mouse',
            isPrimary: true,

            buttons: buttons
        });
    }

    function dispatchToTarget(target, event) {

        if (!target)
            return;

        target.dispatchEvent(event);
    }

    // ============================================
    // Hover 处理
    // ============================================

    function processHover(target, x, y, buttons) {

        if (virtualMouse.lastTarget === target)
            return;

        // leave
        if (virtualMouse.lastTarget) {

            dispatchToTarget(
                virtualMouse.lastTarget,
                createMouseEvent(
                    'mouseleave',
                    x,
                    y,
                    buttons
                )
            );

            dispatchToTarget(
                virtualMouse.lastTarget,
                createPointerEvent(
                    'pointerleave',
                    x,
                    y,
                    buttons
                )
            );
        }

        // enter
        dispatchToTarget(
            target,
            createMouseEvent(
                'mouseenter',
                x,
                y,
                buttons
            )
        );

        dispatchToTarget(
            target,
            createPointerEvent(
                'pointerenter',
                x,
                y,
                buttons
            )
        );

        virtualMouse.lastTarget = target;
    }

    // ============================================
    // 核心输入
    // ============================================

    function injectMouseMove(x, y, buttons = 0) {

        // 更新状态
        virtualMouse.x = x;
        virtualMouse.y = y;
        virtualMouse.buttons = buttons;

        // 自动归一化
        normalize(x, y);

        // 自动命中目标
        const target =
            document.elementFromPoint(x, y) ||
            document.body;

        virtualMouse.currentTarget = target;

        // hover
        processHover(
            target,
            x,
            y,
            buttons
        );

        // mousemove
        dispatchToTarget(
            target,
            createMouseEvent(
                'mousemove',
                x,
                y,
                buttons
            )
        );

        // pointermove
        dispatchToTarget(
            target,
            createPointerEvent(
                'pointermove',
                x,
                y,
                buttons
            )
        );
    }

    function injectMouseDown(x, y, buttons = 1) {

        const target =
            document.elementFromPoint(x, y) ||
            document.body;

        dispatchToTarget(
            target,
            createMouseEvent(
                'mousedown',
                x,
                y,
                buttons
            )
        );

        dispatchToTarget(
            target,
            createPointerEvent(
                'pointerdown',
                x,
                y,
                buttons
            )
        );
    }

    function injectMouseUp(x, y, buttons = 0) {

        const target =
            document.elementFromPoint(x, y) ||
            document.body;

        dispatchToTarget(
            target,
            createMouseEvent(
                'mouseup',
                x,
                y,
                buttons
            )
        );

        dispatchToTarget(
            target,
            createPointerEvent(
                'pointerup',
                x,
                y,
                buttons
            )
        );
    }

    // ============================================
    // 全局 API
    // ============================================

    window.mouseMove = injectMouseMove;
    window.mouseDown = injectMouseDown;
    window.mouseUp = injectMouseUp;

})();