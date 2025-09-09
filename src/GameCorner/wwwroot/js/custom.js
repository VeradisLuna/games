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