window.setInputValue = (el, val) => { if (el) el.value = val; };

window.lunaFocus = {
    focus: function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.focus();
            // optional: keep caret sane and avoid scroll jumps
            el.setSelectionRange?.(1, 1);
            el.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
        }
    }
};

window.mini = {
    selectAll(el) {
        if (!el) return;
        // Select the single character so the next keystroke replaces it
        if (typeof el.select === "function") el.select();
        if (typeof el.setSelectionRange === "function") el.setSelectionRange(0, 1);
    }
};

window.miniTabs = {
    enable: function (id, dotNetRef) {
        const container = document.getElementById(id);
        if (!container) return;

        const handler = (e) => {
            if (e.key === 'Tab') {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnTab', !!e.shiftKey);
            }
        };
        container._miniTabsHandler = handler;
        container.addEventListener('keydown', handler, true);
    },
    disable: function (id) {
        const container = document.getElementById(id);
        if (container && container._miniTabsHandler) {
            container.removeEventListener('keydown', container._miniTabsHandler, true);
            delete container._miniTabsHandler;
        }
    }
};

window.miniDevice = (function () {
    function isMobileLike() {
        const vv = window.visualViewport;
        const width = vv ? vv.width : window.innerWidth;

        const touch = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);
        const ua = navigator.userAgent || "";
        const uaMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua);

        const smallViewport = width < 768;

        return smallViewport && (touch || uaMobile);
    }

    function subscribe(dotNetRef) {
        const fire = () => dotNetRef.invokeMethodAsync('SetOnscreenKeys', isMobileLike());
        window.addEventListener('resize', fire);
        window.addEventListener('orientationchange', fire);
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', fire);
            window.visualViewport.addEventListener('scroll', fire);
        }
        fire();
        window._miniDeviceHandlers = { fire };
    }

    function unsubscribe() {
        const h = window._miniDeviceHandlers;
        if (!h) return;
        window.removeEventListener('resize', h.fire);
        window.removeEventListener('orientationchange', h.fire);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener('resize', h.fire);
            window.visualViewport.removeEventListener('scroll', h.fire);
        }
        delete window._miniDeviceHandlers;
    }

    return { isMobileLike, subscribe, unsubscribe };
})();