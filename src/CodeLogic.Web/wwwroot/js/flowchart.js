// Movable flowchart nodes with connectors that follow them.
//
// Moving a node only changes where it is drawn. The C# source, the trace, and every node ID stay
// exactly as they were; "Auto layout" restores Mermaid's computed positions.

// ---------------------------------------------------------------------------------------------
// Pure geometry (no DOM): tested with node:test.
// A box is { cx, cy, width, height } and a point is { x, y }, all in the edge layer's coordinates.
// ---------------------------------------------------------------------------------------------

/** Pointer movement (in screen pixels) below this is a click that selects, not a drag. */
export const dragThreshold = 4;

export function isDrag(dx, dy, threshold = dragThreshold) {
    return Math.hypot(dx, dy) >= threshold;
}

/**
 * Exit and entry sides from where the two nodes are: bottom-to-top when the target is below,
 * side-to-side when it is beside, and around the right side when the target is above (a loop back).
 */
export function chooseSides(source, target) {
    const dx = target.cx - source.cx;
    const dy = target.cy - source.cy;
    const verticalGap = Math.abs(dy) - (source.height + target.height) / 2;
    const horizontalGap = Math.abs(dx) - (source.width + target.width) / 2;

    if (dy > 0 && verticalGap >= 12 && verticalGap >= horizontalGap) {
        return { exit: "bottom", entry: "top" };
    }
    if (horizontalGap >= 12) {
        return dx > 0 ? { exit: "right", entry: "left" } : { exit: "left", entry: "right" };
    }
    if (dy < 0) {
        return { exit: "right", entry: "right" };
    }
    return { exit: "bottom", entry: "top" };
}

export function portOf(box, side) {
    switch (side) {
        case "top": return { x: box.cx, y: box.cy - box.height / 2 };
        case "bottom": return { x: box.cx, y: box.cy + box.height / 2 };
        case "left": return { x: box.cx - box.width / 2, y: box.cy };
        default: return { x: box.cx + box.width / 2, y: box.cy };
    }
}

function crossesBox(a, b, box, padding = 6) {
    const left = box.cx - box.width / 2 - padding;
    const right = box.cx + box.width / 2 + padding;
    const top = box.cy - box.height / 2 - padding;
    const bottom = box.cy + box.height / 2 + padding;
    if (a.y === b.y) {
        return a.y > top && a.y < bottom && Math.max(a.x, b.x) > left && Math.min(a.x, b.x) < right;
    }
    return a.x > left && a.x < right && Math.max(a.y, b.y) > top && Math.min(a.y, b.y) < bottom;
}

// Tries the preferred coordinate, then steps away from it on both sides, until the segment built
// from it crosses no obstacle. Gives up gracefully (keeps the preferred line) after a few tries.
function clearLine(preferred, step, makeSegment, obstacles) {
    for (let attempt = 0; attempt <= 10; attempt++) {
        const offset = Math.ceil(attempt / 2) * step * (attempt % 2 === 0 ? -1 : 1);
        const [a, b] = makeSegment(preferred + offset);
        if (!obstacles.some(box => crossesBox(a, b, box))) {
            return preferred + offset;
        }
    }
    return preferred;
}

/**
 * An orthogonal route (a list of points) from source to target that avoids crossing the
 * obstacle boxes where it can by moving its middle segment.
 */
export function routeOrthogonal(source, target, obstacles = [], gap = 24) {
    const { exit, entry } = chooseSides(source, target);
    const start = portOf(source, exit);
    const end = portOf(target, entry);
    let points;

    if (exit === "bottom" && entry === "top") {
        const y = clearLine((start.y + end.y) / 2, gap / 2, y => [{ x: start.x, y }, { x: end.x, y }], obstacles);
        points = [start, { x: start.x, y }, { x: end.x, y }, end];
    } else if (exit === entry) {
        const outer = Math.max(start.x, end.x) + gap;
        const x = clearLine(outer, gap, x => [{ x, y: start.y }, { x, y: end.y }], obstacles.map(box => ({ ...box })));
        points = [start, { x: Math.max(x, outer), y: start.y }, { x: Math.max(x, outer), y: end.y }, end];
    } else {
        const x = clearLine((start.x + end.x) / 2, gap / 2, x => [{ x, y: start.y }, { x, y: end.y }], obstacles);
        points = [start, { x, y: start.y }, { x, y: end.y }, end];
    }

    return simplify(points);
}

// Drops repeated points and points in the middle of a straight run.
export function simplify(points) {
    const unique = points.filter((point, index) =>
        index === 0 || Math.abs(point.x - points[index - 1].x) > 0.01 || Math.abs(point.y - points[index - 1].y) > 0.01);
    return unique.filter((point, index) => {
        if (index === 0 || index === unique.length - 1) {
            return true;
        }
        const before = unique[index - 1];
        const after = unique[index + 1];
        const vertical = Math.abs(before.x - point.x) < 0.01 && Math.abs(after.x - point.x) < 0.01;
        const horizontal = Math.abs(before.y - point.y) < 0.01 && Math.abs(after.y - point.y) < 0.01;
        return !vertical && !horizontal;
    });
}

export function pathData(points) {
    return points.map((point, index) => `${index === 0 ? "M" : "L"}${round(point.x)},${round(point.y)}`).join("");
}

/** Where a connector's label sits: the middle of its longest segment, so it stays on the line. */
export function labelAnchor(points) {
    let best = { length: -1, x: points[0].x, y: points[0].y };
    for (let index = 1; index < points.length; index++) {
        const a = points[index - 1];
        const b = points[index];
        const length = Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
        if (length > best.length) {
            best = { length, x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
        }
    }
    return { x: best.x, y: best.y };
}

/** Parses Mermaid's connector id ("L_n003_n006_0") into its node ids. */
export function edgeEnds(dataId) {
    const match = /^L_(.+?)_(.+)_\d+$/.exec(dataId ?? "");
    return match === null ? null : { from: match[1], to: match[2] };
}

function round(value) {
    return Math.round(value * 100) / 100;
}

// ---------------------------------------------------------------------------------------------
// DOM layer.
// ---------------------------------------------------------------------------------------------

const layouts = new WeakMap();

/**
 * Called after every render. When the same graph is drawn again (Play, FULL, zoom, ...), the
 * positions the learner chose are put back; a different graph starts from the computed layout.
 */
export function prepareLayout(container, definition) {
    const previous = layouts.get(container);
    const state = {
        definition,
        offsets: previous?.definition === definition ? previous.offsets : new Map(),
        originals: new Map()
    };
    layouts.set(container, state);
    for (const mermaidId of state.offsets.keys()) {
        placeNode(container, state, mermaidId);
    }
    if (state.offsets.size > 0) {
        rerouteEdges(container, state, new Set(state.offsets.keys()));
    }
}

/** Auto layout: every moved node, connector, and label goes back to Mermaid's computed position. */
export function resetLayout(container) {
    const state = layouts.get(container);
    if (state === undefined) {
        return 0;
    }
    const moved = state.offsets.size;
    for (const [element, original] of state.originals) {
        if (original.value === null) {
            element.removeAttribute(original.attribute);
        } else {
            element.setAttribute(original.attribute, original.value);
        }
        element.classList.remove("flowchart-link--rerouted");
    }
    state.originals.clear();
    state.offsets.clear();
    return moved;
}

export function movedNodeCount(container) {
    return layouts.get(container)?.offsets.size ?? 0;
}

/** Moves a node by (dx, dy) in its own coordinate space and re-routes its connectors. */
export function moveNodeBy(container, mermaidId, dx, dy) {
    const state = layouts.get(container);
    if (state === undefined) {
        return;
    }
    const offset = state.offsets.get(mermaidId) ?? { dx: 0, dy: 0 };
    state.offsets.set(mermaidId, { dx: offset.dx + dx, dy: offset.dy + dy });
    placeNode(container, state, mermaidId);
    rerouteEdges(container, state, new Set([mermaidId]));
}

/**
 * Press, hold, and drag a node to move it. A press without movement stays a click (select).
 * Arrow keys move a focused node (Shift for larger steps).
 */
export function enableNodeDragging(container, signal, onMoved) {
    let drag = null;
    let suppressClick = false;

    container.addEventListener("pointerdown", event => {
        const node = event.target.closest?.(".node");
        const mermaidId = node === null || node === undefined ? null : mermaidIdOf(node);
        if (event.button !== 0 || mermaidId === null) {
            return;
        }
        const scale = node.parentNode.getScreenCTM()?.a || 1;
        drag = { node, mermaidId, x: event.clientX, y: event.clientY, scale, last: { x: 0, y: 0 }, moving: false, pointerId: event.pointerId };
    }, { signal });

    container.addEventListener("pointermove", event => {
        if (drag === null) {
            return;
        }
        const dx = event.clientX - drag.x;
        const dy = event.clientY - drag.y;
        if (!drag.moving) {
            if (!isDrag(dx, dy)) {
                return;
            }
            drag.moving = true;
            drag.node.setPointerCapture?.(drag.pointerId);
            drag.node.classList.add("flowchart-node--dragging");
            container.classList.add("flowchart-canvas--dragging-node");
        }
        event.preventDefault();
        const next = { x: dx / drag.scale, y: dy / drag.scale };
        moveNodeBy(container, drag.mermaidId, next.x - drag.last.x, next.y - drag.last.y);
        drag.last = next;
    }, { signal });

    const finish = () => {
        if (drag?.moving) {
            suppressClick = true;
            window.setTimeout(() => { suppressClick = false; }, 0);
            drag.node.classList.remove("flowchart-node--dragging");
            container.classList.remove("flowchart-canvas--dragging-node");
            onMoved?.(drag.mermaidId);
        }
        drag = null;
    };
    container.addEventListener("pointerup", finish, { signal });
    container.addEventListener("pointercancel", finish, { signal });

    // The click that ends a drag must not also select the node.
    container.addEventListener("click", event => {
        if (suppressClick && event.target.closest?.(".node")) {
            event.stopPropagation();
            event.preventDefault();
            suppressClick = false;
        }
    }, { capture: true, signal });

    container.addEventListener("keydown", event => {
        const node = event.target.closest?.(".node");
        const mermaidId = node === null || node === undefined ? null : mermaidIdOf(node);
        const directions = { ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0] };
        if (mermaidId === null || !(event.key in directions) || event.ctrlKey || event.altKey || event.metaKey) {
            return;
        }
        event.preventDefault();
        event.stopPropagation();
        const step = event.shiftKey ? 50 : 10;
        const [x, y] = directions[event.key];
        moveNodeBy(container, mermaidId, x * step, y * step);
        onMoved?.(mermaidId);
    }, { capture: true, signal });
}

function mermaidIdOf(node) {
    const match = /-flowchart-(n\d+)-/.exec(node.id ?? "");
    return match === null ? null : match[1];
}

function nodeElement(container, mermaidId) {
    return container.querySelector(`.node[id*="${CSS.escape(`-flowchart-${mermaidId}-`)}"]`);
}

function remember(state, element, attribute) {
    if (!state.originals.has(element)) {
        state.originals.set(element, { attribute, value: element.getAttribute(attribute) });
    }
}

function originalOf(state, element, attribute) {
    return state.originals.get(element)?.value ?? element.getAttribute(attribute);
}

// Mermaid positions each node with translate(cx, cy); the offset is added to that original.
function placeNode(container, state, mermaidId) {
    const node = nodeElement(container, mermaidId);
    if (node === null) {
        return;
    }
    remember(state, node, "transform");
    const original = /translate\(\s*([-\d.e]+)[ ,]+([-\d.e]+)\s*\)/.exec(originalOf(state, node, "transform") ?? "");
    const baseX = original === null ? 0 : Number.parseFloat(original[1]);
    const baseY = original === null ? 0 : Number.parseFloat(original[2]);
    const offset = state.offsets.get(mermaidId);
    node.setAttribute("transform", `translate(${round(baseX + offset.dx)}, ${round(baseY + offset.dy)})`);
}

// A node's box expressed in the coordinate space of another element (the connector layer).
function boxIn(space, node) {
    const box = node.getBBox();
    const matrix = space.getCTM().inverse().multiply(node.getCTM());
    const svg = node.ownerSVGElement;
    const corner = (x, y) => {
        const point = svg.createSVGPoint();
        point.x = x;
        point.y = y;
        return point.matrixTransform(matrix);
    };
    const a = corner(box.x, box.y);
    const b = corner(box.x + box.width, box.y + box.height);
    return { cx: (a.x + b.x) / 2, cy: (a.y + b.y) / 2, width: Math.abs(b.x - a.x), height: Math.abs(b.y - a.y) };
}

// Only connectors touching a moved node are re-routed; the rest keep Mermaid's lines.
function rerouteEdges(container, state, movedIds) {
    const svg = container.querySelector(".flowchart-svg");
    if (svg === null) {
        return;
    }
    const edges = [...svg.querySelectorAll("path.flowchart-link")]
        .map(path => ({ path, ends: edgeEnds(path.getAttribute("data-id")) }))
        .filter(edge => edge.ends !== null && (movedIds.has(edge.ends.from) || movedIds.has(edge.ends.to)));
    if (edges.length === 0) {
        return;
    }

    const space = edges[0].path.parentNode;
    const boxes = new Map();
    for (const node of svg.querySelectorAll(".node")) {
        const id = mermaidIdOf(node);
        if (id !== null) {
            boxes.set(id, boxIn(space, node));
        }
    }

    for (const { path, ends } of edges) {
        const source = boxes.get(ends.from);
        const target = boxes.get(ends.to);
        if (source === undefined || target === undefined) {
            continue;
        }
        const obstacles = [...boxes].filter(([id]) => id !== ends.from && id !== ends.to).map(([, box]) => box);
        const points = routeOrthogonal(source, target, obstacles);
        remember(state, path, "d");
        path.setAttribute("d", pathData(points));
        path.classList.add("flowchart-link--rerouted");
        moveLabel(svg, state, path.getAttribute("data-id"), space, labelAnchor(points));
    }
}

function moveLabel(svg, state, dataId, space, anchor) {
    const inner = svg.querySelector(`.edgeLabel .label[data-id="${CSS.escape(dataId)}"]`);
    const label = inner?.closest(".edgeLabel");
    if (label === null || label === undefined) {
        return;
    }
    const labelSpace = label.parentNode;
    const point = svg.createSVGPoint();
    point.x = anchor.x;
    point.y = anchor.y;
    const local = point.matrixTransform(labelSpace.getCTM().inverse().multiply(space.getCTM()));
    remember(state, label, "transform");
    label.setAttribute("transform", `translate(${round(local.x)}, ${round(local.y)})`);
}
