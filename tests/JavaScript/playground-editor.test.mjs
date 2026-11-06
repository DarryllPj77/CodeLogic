import assert from "node:assert/strict";
import test from "node:test";

import {
    createIndentEdit,
    resolveEditorTabKey,
    saveDraft
} from "../../src/CodeLogic.Web/wwwroot/js/playground.js";

function apply(value, edit) {
    if (edit === null) {
        return value;
    }

    return value.slice(0, edit.rangeStart) + edit.replacement + value.slice(edit.rangeEnd);
}

test("Tab inserts four spaces at a cursor and preserves its position", () => {
    const edit = createIndentEdit("int value = 1;", 3, 3);

    assert.equal(apply("int value = 1;", edit), "int     value = 1;");
    assert.equal(edit.selectionStart, 7);
    assert.equal(edit.selectionEnd, 7);
});

test("Tab indents every selected line", () => {
    const source = "int first = 1;\nint second = 2;";
    const edit = createIndentEdit(source, 0, source.length);

    assert.equal(apply(source, edit), "    int first = 1;\n    int second = 2;");
    assert.equal(edit.selectionStart, 4);
    assert.equal(edit.selectionEnd, source.length + 8);
});

test("Shift+Tab removes up to four spaces from the current line", () => {
    const source = "   int value = 1;";
    const edit = createIndentEdit(source, 8, 8, true);

    assert.equal(apply(source, edit), "int value = 1;");
    assert.equal(edit.selectionStart, 5);
    assert.equal(edit.selectionEnd, 5);
});

test("Shift+Tab outdents selected lines without changing unindented text", () => {
    const source = "    first\n  second\nthird";
    const edit = createIndentEdit(source, 0, source.length, true);

    assert.equal(apply(source, edit), "first\nsecond\nthird");
    assert.equal(edit.selectionStart, 0);
    assert.equal(edit.selectionEnd, source.length - 6);
});

test("Escape releases indentation so the next Tab can move focus", () => {
    const released = resolveEditorTabKey("Escape", false, true);
    const focusExit = resolveEditorTabKey("Tab", false, released.tabIndentEnabled);
    const nextVisit = resolveEditorTabKey("Tab", false, focusExit.tabIndentEnabled);

    assert.deepEqual(released, { action: "release", tabIndentEnabled: false });
    assert.deepEqual(focusExit, { action: "focus-next", tabIndentEnabled: true });
    assert.deepEqual(nextVisit, { action: "indent", tabIndentEnabled: true });
});

test("browser draft storage accepts 10,000 characters and rejects 10,001", () => {
    const stored = new Map();
    globalThis.window = {
        localStorage: {
            setItem: (key, value) => stored.set(key, value)
        }
    };

    saveDraft("draft", "a".repeat(10_000), 10_000);

    assert.equal(stored.get("draft").length, 10_000);
    assert.throws(
        () => saveDraft("draft", "b".repeat(10_001), 10_000),
        error => error instanceof RangeError && error.message.includes("10,000-character limit"));
    assert.equal(stored.get("draft").length, 10_000);
});
