const workspaceStates = new WeakMap();

const defaults = Object.freeze({
    explorer: 160,
    editor: 410,
    properties: 240,
    bottom: 280
});

const limits = Object.freeze({
    explorer: [120, 300],
    editor: [288, 720],
    properties: [220, 420],
    bottom: [160, 520]
});

export function initializeWorkspace(root, storageKey) {
    disposeWorkspace(root);

    const controller = new AbortController();
    const state = { controller, storageKey, values: { ...defaults } };
    workspaceStates.set(root, state);

    let restored = false;
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? "null");
        if (saved !== null && typeof saved === "object") {
            for (const key of Object.keys(defaults)) {
                if (Number.isFinite(saved[key])) {
                    state.values[key] = clamp(key, saved[key]);
                    restored = true;
                }
            }
        }
    } catch {
        // Restricted or malformed storage falls back to safe defaults.
    }

    applyAll(root, state.values);
    for (const handle of root.querySelectorAll("[data-resize-panel]")) {
        const panel = handle.dataset.resizePanel;
        handle.addEventListener("pointerdown", event => beginResize(event, root, state, handle, panel), { signal: controller.signal });
        handle.addEventListener("dblclick", () => resetPanel(root, state, panel), { signal: controller.signal });
        handle.addEventListener("keydown", event => resizeWithKeyboard(event, root, state, panel), { signal: controller.signal });
    }

    return restored;
}

export function disposeWorkspace(root) {
    const state = workspaceStates.get(root);
    if (state !== undefined) {
        state.controller.abort();
        workspaceStates.delete(root);
    }
    document.body.classList.remove("workspace-is-resizing");
}

export function savePanelPreference(storageKey, name, value) {
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? "{}") ?? {};
        saved[name] = value;
        window.localStorage.setItem(storageKey, JSON.stringify(saved));
    } catch {
        // Layout persistence is optional when local storage is unavailable.
    }
}

function beginResize(event, root, state, handle, panel) {
    if (event.button !== 0 || !(panel in defaults)) {
        return;
    }

    if (event.detail === 2) {
        resetPanel(root, state, panel);
        return;
    }

    handle.setPointerCapture(event.pointerId);
    document.body.classList.add("workspace-is-resizing");
    root.classList.add("ide-workspace--resizing");

    const startX = event.clientX;
    const startY = event.clientY;
    const startValue = state.values[panel];

    const move = moveEvent => {
        const delta = panel === "bottom"
            ? startY - moveEvent.clientY
            : (panel === "properties" ? startX - moveEvent.clientX : moveEvent.clientX - startX);
        updatePanel(root, state, panel, startValue + delta, false);
    };

    const finish = () => {
        handle.removeEventListener("pointermove", move);
        handle.removeEventListener("pointerup", finish);
        handle.removeEventListener("pointercancel", finish);
        document.body.classList.remove("workspace-is-resizing");
        root.classList.remove("ide-workspace--resizing");
        persist(state);
    };

    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", finish, { once: true });
    handle.addEventListener("pointercancel", finish, { once: true });
}

function resizeWithKeyboard(event, root, state, panel) {
    const horizontal = panel !== "bottom";
    const decreaseKey = horizontal ? "ArrowLeft" : "ArrowDown";
    const increaseKey = horizontal ? "ArrowRight" : "ArrowUp";
    if (event.key !== decreaseKey && event.key !== increaseKey) {
        return;
    }

    event.preventDefault();
    const direction = event.key === increaseKey ? 1 : -1;
    const signedDirection = panel === "properties" ? -direction : direction;
    updatePanel(root, state, panel, state.values[panel] + signedDirection * 16, true);
}

function resetPanel(root, state, panel) {
    if (panel in defaults) {
        updatePanel(root, state, panel, defaults[panel], true);
    }
}

function updatePanel(root, state, panel, value, shouldPersist) {
    state.values[panel] = clamp(panel, value);
    apply(root, panel, state.values[panel]);
    const handle = root.querySelector(`[data-resize-panel="${panel}"]`);
    handle?.setAttribute("aria-valuenow", Math.round(state.values[panel]).toString());
    if (shouldPersist) {
        persist(state);
    }
}

function clamp(panel, value) {
    const [minimum, maximum] = limits[panel];
    return Math.min(maximum, Math.max(minimum, Math.round(value)));
}

function applyAll(root, values) {
    for (const [panel, value] of Object.entries(values)) {
        apply(root, panel, value);
        root.querySelector(`[data-resize-panel="${panel}"]`)
            ?.setAttribute("aria-valuenow", Math.round(value).toString());
    }
}

function apply(root, panel, value) {
    root.style.setProperty(`--${panel}-size`, `${value}px`);
}

function persist(state) {
    try {
        const saved = JSON.parse(window.localStorage.getItem(state.storageKey) ?? "{}") ?? {};
        window.localStorage.setItem(state.storageKey, JSON.stringify({ ...saved, ...state.values }));
    } catch {
        // Resizing remains functional without persistence.
    }
}
