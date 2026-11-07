const workspaceStates = new WeakMap();

// Panel sizes in pixels. "editor" is the left column width; "analysis" is the Analysis panel height.
export const panelDefaults = Object.freeze({
    explorer: 180,
    editor: 440,
    properties: 260,
    analysis: 300
});

export const panelLimits = Object.freeze({
    explorer: [120, 300],
    editor: [320, 900],
    properties: [220, 420],
    analysis: [140, 640],
    // Flowchart focus code column; it has no fixed default (CSS uses 40% until it is dragged).
    focus: [320, 1400]
});

/**
 * In Flowchart focus the editor/flowchart splitter sizes the focus code column instead, so each
 * layout keeps its own width.
 */
export function resolvePanel(root, panel) {
    return panel === "editor" && root.classList.contains("ws--codeview") ? "focus" : panel;
}

function isKnownPanel(panel) {
    return panel in panelLimits;
}

// Handles where dragging toward the start (left/up) makes the panel larger.
const reversedPanels = new Set(["properties", "analysis"]);
const verticalPanels = new Set(["analysis"]);

export function initializeWorkspace(root, storageKey, dotNetReference) {
    disposeWorkspace(root);

    const controller = new AbortController();
    const state = { controller, storageKey, values: { ...panelDefaults } };
    workspaceStates.set(root, state);

    let restored = false;
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? "null");
        if (saved !== null && typeof saved === "object") {
            for (const key of Object.keys(panelLimits)) {
                if (Number.isFinite(saved[key])) {
                    state.values[key] = clampPanel(key, saved[key]);
                    restored = true;
                }
            }
        }
    } catch {
        // Restricted or malformed storage falls back to safe defaults.
    }

    applyAll(root, state.values);

    // Delegated listeners keep working for handles that Blazor shows or re-renders later.
    const handleFrom = event => event.target.closest?.("[data-resize-panel]");
    root.addEventListener("pointerdown", event => {
        const handle = handleFrom(event);
        if (handle !== null && handle !== undefined) {
            beginResize(event, root, state, handle, resolvePanel(root, handle.dataset.resizePanel));
        }
    }, { signal: controller.signal });
    root.addEventListener("dblclick", event => {
        const handle = handleFrom(event);
        if (handle !== null && handle !== undefined) {
            resetPanel(root, state, resolvePanel(root, handle.dataset.resizePanel));
        }
    }, { signal: controller.signal });
    root.addEventListener("keydown", event => {
        const handle = handleFrom(event);
        if (handle !== null && handle !== undefined && event.target === handle) {
            resizeWithKeyboard(event, root, state, resolvePanel(root, handle.dataset.resizePanel));
        }
    }, { signal: controller.signal });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && !event.defaultPrevented && shouldHandleWorkspaceEscape(event.target)) {
            dotNetReference?.invokeMethodAsync("HandleWorkspaceEscape");
        }
    }, { signal: controller.signal });

    return restored;
}

/**
 * Esc restores FULL panels and closes drawers, but not while the learner is typing: Monaco uses Esc
 * to release Tab for keyboard navigation, and form fields use Esc for their own behavior.
 */
export function shouldHandleWorkspaceEscape(target) {
    if (target === null || target === undefined || typeof target.closest !== "function") {
        return true;
    }
    if (target.closest(".code-editor") !== null) {
        return false;
    }
    const tag = String(target.tagName ?? "").toUpperCase();
    return !(tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || target.isContentEditable === true);
}

export function disposeWorkspace(root) {
    const state = workspaceStates.get(root);
    if (state !== undefined) {
        state.controller.abort();
        workspaceStates.delete(root);
    }
    document.body.classList.remove("workspace-is-resizing");
}

export function loadPanelPreferences(storageKey) {
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? "null");
        if (saved === null || typeof saved !== "object") {
            return null;
        }
        return {
            mode: typeof saved.mode === "string" ? saved.mode : null,
            editorTheme: typeof saved.editorTheme === "string" ? saved.editorTheme : null,
            explorerOpen: typeof saved.explorerOpen === "boolean" ? saved.explorerOpen : null
        };
    } catch {
        return null;
    }
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

export function resetPanelSizes(root) {
    const state = workspaceStates.get(root);
    if (state === undefined) {
        return;
    }
    state.values = { ...panelDefaults };
    root.style.removeProperty("--focus-size");
    applyAll(root, state.values);
    persist(state, ["focus"]);
}

export function clampPanel(panel, value) {
    const [minimum, maximum] = panelLimits[panel];
    return Math.min(maximum, Math.max(minimum, Math.round(value)));
}

function beginResize(event, root, state, handle, panel) {
    if (event.button !== 0 || !isKnownPanel(panel)) {
        return;
    }

    if (event.detail === 2) {
        resetPanel(root, state, panel);
        return;
    }

    event.preventDefault();
    handle.setPointerCapture(event.pointerId);
    document.body.classList.add("workspace-is-resizing");
    root.classList.add("ws--resizing");

    const startX = event.clientX;
    const startY = event.clientY;
    const startValue = currentSize(root, state, panel);
    const direction = reversedPanels.has(panel) ? -1 : 1;

    const move = moveEvent => {
        const delta = verticalPanels.has(panel)
            ? moveEvent.clientY - startY
            : moveEvent.clientX - startX;
        updatePanel(root, state, panel, startValue + direction * delta, false);
    };

    const finish = () => {
        handle.removeEventListener("pointermove", move);
        handle.removeEventListener("pointerup", finish);
        handle.removeEventListener("pointercancel", finish);
        document.body.classList.remove("workspace-is-resizing");
        root.classList.remove("ws--resizing");
        persist(state);
    };

    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", finish, { once: true });
    handle.addEventListener("pointercancel", finish, { once: true });
}

function resizeWithKeyboard(event, root, state, panel) {
    if (!isKnownPanel(panel)) {
        return;
    }
    const vertical = verticalPanels.has(panel);
    const decreaseKey = vertical ? "ArrowDown" : "ArrowLeft";
    const increaseKey = vertical ? "ArrowUp" : "ArrowRight";
    if (event.key !== decreaseKey && event.key !== increaseKey) {
        return;
    }

    event.preventDefault();
    const direction = event.key === increaseKey ? 1 : -1;
    // ArrowUp grows the bottom Analysis panel; ArrowLeft grows the right-anchored properties drawer.
    const signedDirection = panel === "properties" ? -direction : direction;
    updatePanel(root, state, panel, currentSize(root, state, panel) + signedDirection * 16, true);
}

// The focus column starts from its measured width (40% of the work area) until it is dragged.
function currentSize(root, state, panel) {
    return state.values[panel] ?? root.querySelector(".ws-left")?.getBoundingClientRect().width ?? panelLimits[panel][0];
}

function resetPanel(root, state, panel) {
    if (panel === "focus") {
        delete state.values.focus;
        root.style.removeProperty("--focus-size");
        persist(state, ["focus"]);
        return;
    }

    if (panel in panelDefaults) {
        updatePanel(root, state, panel, panelDefaults[panel], true);
    }
}

function updatePanel(root, state, panel, value, shouldPersist) {
    state.values[panel] = clampPanel(panel, value);
    apply(root, panel, state.values[panel]);
    if (shouldPersist) {
        persist(state);
    }
}

function applyAll(root, values) {
    for (const [panel, value] of Object.entries(values)) {
        apply(root, panel, value);
    }
}

function apply(root, panel, value) {
    root.style.setProperty(`--${panel}-size`, `${value}px`);
    for (const handle of root.querySelectorAll(`[data-resize-panel="${panel}"]`)) {
        handle.setAttribute("aria-valuenow", Math.round(value).toString());
    }
}

function persist(state, removedKeys = []) {
    try {
        const saved = JSON.parse(window.localStorage.getItem(state.storageKey) ?? "{}") ?? {};
        for (const key of removedKeys) {
            delete saved[key];
        }
        window.localStorage.setItem(state.storageKey, JSON.stringify({ ...saved, ...state.values }));
    } catch {
        // Resizing remains functional without persistence.
    }
}
