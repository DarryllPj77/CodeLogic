const shortcutHandlers = new WeakMap();

export function initializeEditorShortcuts(editor) {
    const handler = event => {
        const key = event.key.toLowerCase();
        let command = null;

        if (event.ctrlKey && !event.altKey && !event.shiftKey && key === "s") {
            command = "save";
        } else if (event.ctrlKey && event.altKey && !event.shiftKey && key === "r") {
            command = "reset";
        }

        if (command === null) {
            return;
        }

        event.preventDefault();
    };

    editor.addEventListener("keydown", handler);
    shortcutHandlers.set(editor, handler);
}

export function disposeEditorShortcuts(editor) {
    const handler = shortcutHandlers.get(editor);
    if (handler !== undefined) {
        editor.removeEventListener("keydown", handler);
        shortcutHandlers.delete(editor);
    }
}

export function loadDraft(storageKey) {
    return window.localStorage.getItem(storageKey);
}

export function saveDraft(storageKey, code) {
    window.localStorage.setItem(storageKey, code);
}

export function clearDraft(storageKey) {
    window.localStorage.removeItem(storageKey);
}
