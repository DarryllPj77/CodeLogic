// Build Flowchart: a free-form diagram canvas.
//
// Builder diagrams are drawings only. They never read or change the C# source, the analyzer output,
// or the trace, and they are saved as JSON in this browser under their own storage key.
//
// The first half of this file is a pure model (no DOM) so node:test can cover it. Every operation
// returns a new diagram object, which makes undo and redo a simple list of snapshots.

import { chooseSides, edgeScrollSpeed, growBounds, isDrag, labelAnchor, pathData, portOf, routeOrthogonal } from "./flowchart.js?v=2";
import {
    commit,
    createHistory,
    dashArray,
    effectiveStyle,
    fontFamilies,
    handleFormatShortcut,
    mergeStyle,
    normalizeStyle,
    notifyFormatChanged,
    ptToPx,
    redo,
    registerFormatTarget,
    removeStyleKeys,
    textAnchor,
    textDecoration,
    textPadding,
    undo,
    unregisterFormatTarget
} from "./format.js?v=1";

// Undo/redo snapshots live in format.js and are shared with the graph's formatting.
export { commit, createHistory, redo, undo };

// ---------------------------------------------------------------------------------------------
// Catalog
// ---------------------------------------------------------------------------------------------

export const diagramVersion = 1;

/** Limits keep a saved (or tampered) draft from growing without bound. */
export const builderLimits = Object.freeze({ shapes: 500, connectors: 1000, text: 500, scratchpad: 40, size: 2000, coordinate: 1_000_000 });

export const shapeCatalog = Object.freeze([
    { kind: "rectangle", label: "Rectangle", width: 120, height: 60, keywords: "box step" },
    { kind: "rounded", label: "Rounded rectangle", width: 120, height: 60, keywords: "box start end terminal" },
    { kind: "text", label: "Text", width: 100, height: 30, text: "Text", keywords: "label note" },
    { kind: "heading", label: "Heading", width: 220, height: 40, text: "Heading", keywords: "title" },
    { kind: "ellipse", label: "Ellipse", width: 120, height: 70, keywords: "oval start end terminal" },
    { kind: "square", label: "Square", width: 80, height: 80, keywords: "box" },
    { kind: "circle", label: "Circle", width: 80, height: 80, keywords: "connector round" },
    { kind: "process", label: "Process", width: 130, height: 60, keywords: "subroutine predefined method call" },
    { kind: "decision", label: "Decision", width: 120, height: 90, keywords: "diamond if condition branch" },
    { kind: "parallelogram", label: "Input / output", width: 140, height: 60, keywords: "parallelogram io data read write console" },
    { kind: "hexagon", label: "Hexagon", width: 120, height: 70, keywords: "preparation loop" },
    { kind: "triangle", label: "Triangle", width: 90, height: 80, keywords: "merge" },
    { kind: "cylinder", label: "Cylinder", width: 80, height: 90, keywords: "database storage" },
    { kind: "cloud", label: "Cloud", width: 130, height: 80, keywords: "internet network" },
    { kind: "document", label: "Document", width: 120, height: 80, keywords: "file report" },
    { kind: "card", label: "Card", width: 110, height: 70, keywords: "punched card" },
    { kind: "data", label: "Data", width: 130, height: 60, keywords: "stored data" },
    { kind: "actor", label: "Actor", width: 40, height: 80, keywords: "person user stick figure" }
].map(Object.freeze));

/** Connector styles. Every style ends in an arrowhead; "arrow" uses orthogonal smart routing. */
export const connectorCatalog = Object.freeze([
    { kind: "straight", label: "Straight line", keywords: "direct line" },
    { kind: "dashed", label: "Dashed line", keywords: "dotted optional" },
    { kind: "curved", label: "Curved line", keywords: "bezier curve" },
    { kind: "arrow", label: "Arrow (smart routing)", keywords: "orthogonal elbow routed" }
].map(Object.freeze));

export const defaultConnectorKind = "arrow";

const shapeKinds = new Set(shapeCatalog.map(item => item.kind));
const connectorKinds = new Set(connectorCatalog.map(item => item.kind));

export function catalogItem(kind) {
    return shapeCatalog.find(item => item.kind === kind) ?? null;
}

/** Search box: matches the label, the kind, or a keyword, ignoring case. An empty query shows all. */
export function filterCatalog(items, query) {
    const words = String(query ?? "").trim().toLowerCase().split(/\s+/).filter(Boolean);
    if (words.length === 0) {
        return [...items];
    }
    return items.filter(item => {
        const haystack = `${item.label} ${item.kind} ${item.keywords ?? ""}`.toLowerCase();
        return words.every(word => haystack.includes(word));
    });
}

// ---------------------------------------------------------------------------------------------
// Shape outlines (local coordinates: 0..width, 0..height)
// ---------------------------------------------------------------------------------------------

// Scales a path written in unit coordinates (absolute M/L/C/Z only; numbers alternate x, y).
function unitPath(d, width, height) {
    let index = 0;
    return d.replace(/-?\d*\.?\d+/g, value => {
        const scaled = Number.parseFloat(value) * (index++ % 2 === 0 ? width : height);
        return String(round(scaled));
    });
}

function rectPath(width, height) {
    return `M0,0L${round(width)},0L${round(width)},${round(height)}L0,${round(height)}Z`;
}

function ellipsePath(cx, cy, rx, ry) {
    return `M${round(cx - rx)},${round(cy)}A${round(rx)},${round(ry)} 0 1,0 ${round(cx + rx)},${round(cy)}A${round(rx)},${round(ry)} 0 1,0 ${round(cx - rx)},${round(cy)}Z`;
}

/**
 * The outline of a shape: `d` is the filled body, `details` are extra strokes (such as a cylinder's
 * rim), and `outlined: false` marks text-only shapes whose body is an invisible hit area.
 */
export function shapeOutline(kind, width, height) {
    const w = width;
    const h = height;
    switch (kind) {
        case "rounded": {
            const r = round(Math.min(12, w / 4, h / 4));
            return { d: `M${r},0L${round(w - r)},0A${r},${r} 0 0,1 ${round(w)},${r}L${round(w)},${round(h - r)}A${r},${r} 0 0,1 ${round(w - r)},${round(h)}L${r},${round(h)}A${r},${r} 0 0,1 0,${round(h - r)}L0,${r}A${r},${r} 0 0,1 ${r},0Z`, details: [], outlined: true };
        }
        case "text":
        case "heading":
            return { d: rectPath(w, h), details: [], outlined: false };
        case "ellipse":
        case "circle":
            return { d: ellipsePath(w / 2, h / 2, w / 2, h / 2), details: [], outlined: true };
        case "process": {
            const inset = round(Math.min(12, w / 8));
            return { d: rectPath(w, h), details: [`M${inset},0L${inset},${round(h)}`, `M${round(w - inset)},0L${round(w - inset)},${round(h)}`], outlined: true };
        }
        case "decision":
            return { d: `M${round(w / 2)},0L${round(w)},${round(h / 2)}L${round(w / 2)},${round(h)}L0,${round(h / 2)}Z`, details: [], outlined: true };
        case "parallelogram": {
            const s = round(Math.min(22, w / 4));
            return { d: `M${s},0L${round(w)},0L${round(w - s)},${round(h)}L0,${round(h)}Z`, details: [], outlined: true };
        }
        case "hexagon": {
            const s = round(Math.min(22, w / 4));
            return { d: `M${s},0L${round(w - s)},0L${round(w)},${round(h / 2)}L${round(w - s)},${round(h)}L${s},${round(h)}L0,${round(h / 2)}Z`, details: [], outlined: true };
        }
        case "triangle":
            return { d: `M${round(w / 2)},0L${round(w)},${round(h)}L0,${round(h)}Z`, details: [], outlined: true };
        case "cylinder": {
            const ry = round(Math.min(10, h / 6));
            const rx = round(w / 2);
            return {
                d: `M0,${ry}A${rx},${ry} 0 0,1 ${round(w)},${ry}L${round(w)},${round(h - ry)}A${rx},${ry} 0 0,1 0,${round(h - ry)}Z`,
                details: [`M0,${ry}A${rx},${ry} 0 0,0 ${round(w)},${ry}`],
                outlined: true
            };
        }
        case "cloud":
            return { d: unitPath("M0.25,0.82C0.05,0.82 0.02,0.55 0.18,0.5C0.12,0.28 0.32,0.16 0.45,0.27C0.53,0.08 0.8,0.1 0.8,0.34C0.98,0.34 1,0.62 0.86,0.7C0.9,0.86 0.74,0.94 0.64,0.86C0.55,0.98 0.3,0.97 0.25,0.82Z", w, h), details: [], outlined: true };
        case "document":
            return { d: unitPath("M0,0L1,0L1,0.84C0.75,0.7 0.5,1 0.25,0.92C0.12,0.88 0.05,0.86 0,0.88Z", w, h), details: [], outlined: true };
        case "card": {
            const c = round(Math.min(18, w / 4, h / 3));
            return { d: `M${c},0L${round(w)},0L${round(w)},${round(h)}L0,${round(h)}L0,${c}Z`, details: [], outlined: true };
        }
        case "data":
            return { d: unitPath("M0.12,0L1,0C0.9,0.15 0.9,0.85 1,1L0.12,1C0,0.85 0,0.15 0.12,0Z", w, h), details: [], outlined: true };
        case "actor": {
            const head = Math.min(w, h) * 0.2;
            const cx = w / 2;
            const neck = head * 2;
            const hip = h * 0.62;
            return {
                d: rectPath(w, h),
                details: [
                    ellipsePath(cx, head, head, head),
                    `M${round(cx)},${round(neck)}L${round(cx)},${round(hip)}`,
                    `M0,${round(h * 0.38)}L${round(w)},${round(h * 0.38)}`,
                    `M${round(cx)},${round(hip)}L0,${round(h)}`,
                    `M${round(cx)},${round(hip)}L${round(w)},${round(h)}`
                ],
                outlined: false
            };
        }
        default:
            return { d: rectPath(w, h), details: [], outlined: true };
    }
}

/**
 * Where a shape's text goes: line by line, aligned and padded by its style. A heading defaults to
 * 20px bold; an actor's label sits below the figure.
 */
export function textLayout(shape) {
    const style = shape.style ?? {};
    const s = effectiveStyle(style);
    const lines = String(shape.text ?? "").split("\n");
    const heading = shape.kind === "heading";
    const fontSize = "fontSize" in style ? s.fontSize : heading ? 20 : 14;
    const bold = "bold" in style ? s.bold : heading;
    const lineHeight = Math.round(fontSize * 1.25);
    const pad = textPadding(style);
    const block = lineHeight * (lines.length - 1);
    const x = s.align === "left" ? pad.left : s.align === "right" ? shape.width - pad.right : shape.width / 2;
    let firstY;
    if (shape.kind === "actor") {
        firstY = shape.height + pad.top + lineHeight / 2;
    } else if (s.vertical === "top") {
        firstY = pad.top + lineHeight / 2;
    } else if (s.vertical === "bottom") {
        firstY = shape.height - pad.bottom - lineHeight / 2 - block;
    } else {
        firstY = shape.height / 2 - block / 2;
    }
    return { lines, fontSize, lineHeight, x, firstY, bold, anchor: textAnchor(s.align, s.direction) };
}

// ---------------------------------------------------------------------------------------------
// Diagram operations (pure)
// ---------------------------------------------------------------------------------------------

export function createDiagram() {
    return { version: diagramVersion, shapes: [], connectors: [], defaultStyle: {} };
}

function nextId(items, prefix) {
    let highest = 0;
    for (const item of items) {
        const match = new RegExp(`^${prefix}(\\d+)$`).exec(item.id);
        if (match !== null) {
            highest = Math.max(highest, Number(match[1]));
        }
    }
    return `${prefix}${highest + 1}`;
}

/** Adds a shape centred on (cx, cy). Returns the new diagram and the shape's id (null at the limit). */
export function addShape(diagram, kind, cx, cy, template = null) {
    const item = catalogItem(kind);
    if (item === null || diagram.shapes.length >= builderLimits.shapes) {
        return { diagram, id: null };
    }
    const width = clampSize(template?.width ?? item.width);
    const height = clampSize(template?.height ?? item.height);
    const shape = {
        id: nextId(diagram.shapes, "s"),
        kind,
        x: round(cx - width / 2),
        y: round(cy - height / 2),
        width,
        height,
        text: limitText(template?.text ?? item.text ?? ""),
        rotation: 0,
        // New shapes start from "Set as Default Style"; a scratchpad copy keeps its own style.
        style: normalizeStyle(template?.style ?? diagram.defaultStyle ?? {})
    };
    return { diagram: { ...diagram, shapes: [...diagram.shapes, shape] }, id: shape.id };
}

export function moveShapes(diagram, ids, dx, dy) {
    const moving = new Set(ids);
    return {
        ...diagram,
        shapes: diagram.shapes.map(shape => moving.has(shape.id) ? { ...shape, x: round(shape.x + dx), y: round(shape.y + dy) } : shape)
    };
}

export function setShapeText(diagram, id, text) {
    return { ...diagram, shapes: diagram.shapes.map(shape => shape.id === id ? { ...shape, text: limitText(text) } : shape) };
}

/** Connects two different shapes. Returns { diagram, id } with id null when the connection is not allowed. */
export function addConnector(diagram, from, to, kind = defaultConnectorKind, label = "") {
    const ids = new Set(diagram.shapes.map(shape => shape.id));
    if (from === to || !ids.has(from) || !ids.has(to) || !connectorKinds.has(kind) || diagram.connectors.length >= builderLimits.connectors) {
        return { diagram, id: null };
    }
    const connector = { id: nextId(diagram.connectors, "c"), from, to, kind, label: limitText(label), style: {} };
    return { diagram: { ...diagram, connectors: [...diagram.connectors, connector] }, id: connector.id };
}

export function setConnectorLabel(diagram, id, label) {
    return { ...diagram, connectors: diagram.connectors.map(item => item.id === id ? { ...item, label: limitText(label) } : item) };
}

export function setConnectorKind(diagram, ids, kind) {
    const wanted = new Set(ids);
    if (!connectorKinds.has(kind)) {
        return diagram;
    }
    return { ...diagram, connectors: diagram.connectors.map(item => wanted.has(item.id) ? { ...item, kind } : item) };
}

/** Deletes shapes and connectors; connectors attached to a deleted shape go with it. */
export function deleteItems(diagram, ids) {
    const removed = new Set(ids);
    const shapes = diagram.shapes.filter(shape => !removed.has(shape.id));
    const kept = new Set(shapes.map(shape => shape.id));
    const connectors = diagram.connectors.filter(item => !removed.has(item.id) && kept.has(item.from) && kept.has(item.to));
    return { ...diagram, shapes, connectors };
}

export function shapeBox(shape) {
    return { cx: shape.x + shape.width / 2, cy: shape.y + shape.height / 2, width: shape.width, height: shape.height };
}

// ---- formatting and arranging (pure) --------------------------------------------------------

function mapItems(diagram, ids, change) {
    const wanted = new Set(ids);
    return {
        ...diagram,
        shapes: diagram.shapes.map(shape => wanted.has(shape.id) ? change(shape) : shape),
        connectors: diagram.connectors.map(item => wanted.has(item.id) ? change(item) : item)
    };
}

/** Merges a style patch into every listed shape and connector (null removes a key). */
export function setStyles(diagram, ids, patch) {
    return mapItems(diagram, ids, item => ({ ...item, style: mergeStyle(item.style, patch) }));
}

/** Replaces the whole style of every listed item (Edit style, Paste Style). */
export function replaceStyles(diagram, ids, style) {
    const clean = normalizeStyle(style);
    return mapItems(diagram, ids, item => ({ ...item, style: { ...clean } }));
}

/** Removes some style keys (Clear Formatting) or all of them (Reset style). */
export function clearStyles(diagram, ids, keys = null) {
    return mapItems(diagram, ids, item => ({ ...item, style: removeStyleKeys(item.style ?? {}, keys) }));
}

export function setDefaultStyle(diagram, style) {
    return { ...diagram, defaultStyle: normalizeStyle(style) };
}

/** Position, size, and rotation from the Arrange tab; invalid numbers are ignored. */
export function setGeometry(diagram, ids, patch) {
    const wanted = new Set(ids);
    const value = key => Number.isFinite(patch[key]) ? patch[key] : undefined;
    return {
        ...diagram,
        shapes: diagram.shapes.map(shape => {
            if (!wanted.has(shape.id)) {
                return shape;
            }
            const next = { ...shape };
            if (value("x") !== undefined) next.x = round(Math.max(-builderLimits.coordinate, Math.min(builderLimits.coordinate, value("x"))));
            if (value("y") !== undefined) next.y = round(Math.max(-builderLimits.coordinate, Math.min(builderLimits.coordinate, value("y"))));
            if (value("width") !== undefined) next.width = clampSize(value("width"));
            if (value("height") !== undefined) next.height = clampSize(value("height"));
            if (value("rotation") !== undefined) next.rotation = normalizeRotation(value("rotation"));
            return next;
        })
    };
}

/** Aligns shapes to the edges or centre of their combined bounds. */
export function alignShapes(diagram, ids, mode) {
    const wanted = new Set(ids);
    const chosen = diagram.shapes.filter(shape => wanted.has(shape.id));
    if (chosen.length < 2) {
        return diagram;
    }
    const left = Math.min(...chosen.map(shape => shape.x));
    const top = Math.min(...chosen.map(shape => shape.y));
    const right = Math.max(...chosen.map(shape => shape.x + shape.width));
    const bottom = Math.max(...chosen.map(shape => shape.y + shape.height));
    const place = shape => {
        switch (mode) {
            case "left": return { x: left };
            case "center": return { x: round((left + right) / 2 - shape.width / 2) };
            case "right": return { x: round(right - shape.width) };
            case "top": return { y: top };
            case "middle": return { y: round((top + bottom) / 2 - shape.height / 2) };
            case "bottom": return { y: round(bottom - shape.height) };
            default: return {};
        }
    };
    return { ...diagram, shapes: diagram.shapes.map(shape => wanted.has(shape.id) ? { ...shape, ...place(shape) } : shape) };
}

/** Spaces three or more shapes so the gaps between them are equal, keeping the outer two in place. */
export function distributeShapes(diagram, ids, axis) {
    const wanted = new Set(ids);
    const horizontal = axis === "horizontal";
    const position = horizontal ? "x" : "y";
    const size = horizontal ? "width" : "height";
    const chosen = diagram.shapes.filter(shape => wanted.has(shape.id)).sort((a, b) => a[position] - b[position]);
    if (chosen.length < 3) {
        return diagram;
    }
    const start = chosen[0][position];
    const end = chosen.at(-1)[position] + chosen.at(-1)[size];
    const gap = (end - start - chosen.reduce((sum, shape) => sum + shape[size], 0)) / (chosen.length - 1);
    const placed = new Map();
    let cursor = start;
    for (const shape of chosen) {
        placed.set(shape.id, round(cursor));
        cursor += shape[size] + gap;
    }
    return { ...diagram, shapes: diagram.shapes.map(shape => placed.has(shape.id) ? { ...shape, [position]: placed.get(shape.id) } : shape) };
}

/** Bring to front draws the shapes last; send to back draws them first. Their order is kept. */
export function reorderShapes(diagram, ids, where) {
    const wanted = new Set(ids);
    const chosen = diagram.shapes.filter(shape => wanted.has(shape.id));
    const others = diagram.shapes.filter(shape => !wanted.has(shape.id));
    return { ...diagram, shapes: where === "back" ? [...chosen, ...others] : [...others, ...chosen] };
}

function normalizeRotation(value) {
    const turned = ((Number(value) % 360) + 360) % 360;
    return round(turned > 180 ? turned - 360 : turned);
}

/**
 * The drawn path of a connector between two shape boxes. "arrow" and "dashed" use the orthogonal
 * smart routing (avoiding other shapes where it can); "straight" and "curved" join the nearest sides.
 */
export function connectorGeometry(source, target, kind, obstacles = []) {
    if (kind === "arrow" || kind === "dashed") {
        const points = routeOrthogonal(source, target, obstacles);
        return { d: pathData(points), label: labelAnchor(points), start: points[0], end: points.at(-1) };
    }
    const { exit, entry } = chooseSides(source, target);
    const start = portOf(source, exit);
    const end = portOf(target, entry);
    if (kind === "curved") {
        const pull = Math.max(40, Math.hypot(end.x - start.x, end.y - start.y) / 3);
        const c1 = pullFrom(start, exit, pull);
        const c2 = pullFrom(end, entry, pull);
        const mid = {
            x: 0.125 * start.x + 0.375 * c1.x + 0.375 * c2.x + 0.125 * end.x,
            y: 0.125 * start.y + 0.375 * c1.y + 0.375 * c2.y + 0.125 * end.y
        };
        return {
            d: `M${round(start.x)},${round(start.y)}C${round(c1.x)},${round(c1.y)} ${round(c2.x)},${round(c2.y)} ${round(end.x)},${round(end.y)}`,
            label: { x: round(mid.x), y: round(mid.y) },
            start,
            end
        };
    }
    return { d: pathData([start, end]), label: { x: round((start.x + end.x) / 2), y: round((start.y + end.y) / 2) }, start, end };
}

function pullFrom(point, side, distance) {
    switch (side) {
        case "top": return { x: point.x, y: point.y - distance };
        case "bottom": return { x: point.x, y: point.y + distance };
        case "left": return { x: point.x - distance, y: point.y };
        default: return { x: point.x + distance, y: point.y };
    }
}

/** A shape's box grown by its Perimeter spacing, so connectors stop short of the outline. */
function perimeterBox(shape) {
    const gap = effectiveStyle(shape.style).perimeter * 2;
    const box = shapeBox(shape);
    return { ...box, width: box.width + gap, height: box.height + gap };
}

/** Geometry for every connector, recomputed from the shapes' current positions. */
export function routeConnectors(diagram) {
    const shapes = new Map(diagram.shapes.map(shape => [shape.id, shape]));
    const routes = new Map();
    for (const connector of diagram.connectors) {
        const source = shapes.get(connector.from);
        const target = shapes.get(connector.to);
        if (source === undefined || target === undefined) {
            continue;
        }
        const obstacles = diagram.shapes.filter(shape => shape.id !== source.id && shape.id !== target.id).map(shapeBox);
        routes.set(connector.id, connectorGeometry(perimeterBox(source), perimeterBox(target), connector.kind, obstacles));
    }
    return routes;
}

/** The area every shape occupies (an actor's label hangs below it), or null for an empty diagram. */
export function diagramBounds(diagram) {
    if (diagram.shapes.length === 0) {
        return null;
    }
    let left = Infinity;
    let top = Infinity;
    let right = -Infinity;
    let bottom = -Infinity;
    for (const shape of diagram.shapes) {
        const below = shape.kind === "actor" ? textLayout(shape).lines.length * 18 + 8 : 0;
        left = Math.min(left, shape.x);
        top = Math.min(top, shape.y);
        right = Math.max(right, shape.x + shape.width);
        bottom = Math.max(bottom, shape.y + shape.height + below);
    }
    return { x: left, y: top, width: right - left, height: bottom - top };
}

// ---------------------------------------------------------------------------------------------
// Save format (JSON). A saved draft is untrusted input: it is validated and size-limited, and text
// is only ever drawn with textContent, never parsed as markup.
// ---------------------------------------------------------------------------------------------

export function serializeDiagram(diagram, scratchpad = []) {
    return JSON.stringify({ version: diagramVersion, shapes: diagram.shapes, connectors: diagram.connectors, defaultStyle: diagram.defaultStyle ?? {}, scratchpad }, null, 2);
}

/** Parses a saved diagram. Returns { diagram, scratchpad } or null when the JSON is not a valid diagram. */
export function parseDiagram(json) {
    let data;
    try {
        data = typeof json === "string" ? JSON.parse(json) : json;
    } catch {
        return null;
    }
    if (data === null || typeof data !== "object" || data.version !== diagramVersion || !Array.isArray(data.shapes) || !Array.isArray(data.connectors)) {
        return null;
    }
    if (data.shapes.length > builderLimits.shapes || data.connectors.length > builderLimits.connectors) {
        return null;
    }
    const shapes = [];
    const seen = new Set();
    for (const raw of data.shapes) {
        const shape = parseShape(raw);
        if (shape === null || seen.has(shape.id)) {
            return null;
        }
        seen.add(shape.id);
        shapes.push(shape);
    }
    const connectors = [];
    for (const raw of data.connectors) {
        if (raw === null || typeof raw !== "object" || !/^c\d{1,6}$/.test(raw.id) || !seen.has(raw.from) || !seen.has(raw.to) || raw.from === raw.to || !connectorKinds.has(raw.kind)) {
            return null;
        }
        connectors.push({ id: raw.id, from: raw.from, to: raw.to, kind: raw.kind, label: limitText(raw.label ?? ""), style: normalizeStyle(raw.style) });
    }
    const scratchpad = (Array.isArray(data.scratchpad) ? data.scratchpad : [])
        .slice(0, builderLimits.scratchpad)
        .map(parseTemplate)
        .filter(item => item !== null);
    return { diagram: { version: diagramVersion, shapes, connectors, defaultStyle: normalizeStyle(data.defaultStyle) }, scratchpad };
}

function parseShape(raw) {
    if (raw === null || typeof raw !== "object" || !/^s\d{1,6}$/.test(raw.id) || !shapeKinds.has(raw.kind)) {
        return null;
    }
    const numbers = [raw.x, raw.y, raw.width, raw.height];
    if (!numbers.every(Number.isFinite) || Math.abs(raw.x) > builderLimits.coordinate || Math.abs(raw.y) > builderLimits.coordinate) {
        return null;
    }
    const rotation = Number.isFinite(raw.rotation) ? normalizeRotation(raw.rotation) : 0;
    return { id: raw.id, kind: raw.kind, x: round(raw.x), y: round(raw.y), width: clampSize(raw.width), height: clampSize(raw.height), text: limitText(raw.text ?? ""), rotation, style: normalizeStyle(raw.style) };
}

/** A scratchpad entry is a reusable shape template: kind, size, and text. */
export function parseTemplate(raw) {
    if (raw === null || typeof raw !== "object" || !shapeKinds.has(raw.kind) || !Number.isFinite(raw.width) || !Number.isFinite(raw.height)) {
        return null;
    }
    return { kind: raw.kind, width: clampSize(raw.width), height: clampSize(raw.height), text: limitText(raw.text ?? ""), style: normalizeStyle(raw.style) };
}

function clampSize(value) {
    return round(Math.min(builderLimits.size, Math.max(10, Number(value) || 10)));
}

function limitText(value) {
    return String(value ?? "").slice(0, builderLimits.text);
}

function round(value) {
    return Math.round(value * 100) / 100;
}

// ---------------------------------------------------------------------------------------------
// DOM layer
// ---------------------------------------------------------------------------------------------

const svgNamespace = "http://www.w3.org/2000/svg";
const minimumZoom = 0.25;
const maximumZoom = 2;
const style = Object.freeze({
    fill: "#13243b",
    stroke: "#4f8fea",
    text: "#e6edf7",
    connector: "#94a3b8",
    background: "#0a1020",
    font: "Inter, 'Segoe UI', system-ui, sans-serif"
});
const builders = new WeakMap();

/** Mounts the builder in a root element rendered by FlowchartBuilder.razor. */
export function mountBuilder(root, storageKey) {
    unmountBuilder(root);
    const builder = new Builder(root, storageKey);
    builders.set(root, builder);
    return builder.diagram.shapes.length;
}

export function unmountBuilder(root) {
    builders.get(root)?.dispose();
    builders.delete(root);
}

/** For tests in the browser: the current diagram, as saved. */
export function builderSnapshot(root) {
    const builder = builders.get(root);
    return builder === undefined ? null : JSON.parse(serializeDiagram(builder.diagram, builder.scratchpad));
}

class Builder {
    constructor(root, storageKey) {
        this.root = root;
        this.storageKey = storageKey;
        this.controller = new AbortController();
        this.canvas = root.querySelector("[data-builder-canvas]");
        this.status = root.querySelector("[data-builder-status]");
        this.outline = root.querySelector("[data-builder-outline]");
        this.zoomOutput = root.querySelector("[data-builder-zoom]");
        this.selection = [];
        this.zoom = 1;
        this.connectorKind = defaultConnectorKind;
        this.editing = null;
        this.saveTimer = 0;
        this.pointerActive = false;

        const saved = parseDiagram(readStorage(storageKey));
        this.history = createHistory(saved?.diagram ?? createDiagram());
        this.scratchpad = saved?.scratchpad ?? [];

        this.svg = document.createElementNS(svgNamespace, "svg");
        this.svg.classList.add("builder-svg");
        this.svg.setAttribute("role", "group");
        this.svg.setAttribute("aria-label", "Diagram canvas");
        this.svg.innerHTML = `<defs><marker id="builder-arrowhead" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M0,0L10,5L0,10Z" fill="${style.connector}"/></marker><filter id="builder-shadow" x="-20%" y="-20%" width="140%" height="150%"><feDropShadow dx="0" dy="3" stdDeviation="3" flood-color="#000" flood-opacity="0.55"/></filter><filter id="builder-text-shadow" x="-10%" y="-20%" width="120%" height="140%"><feDropShadow dx="0" dy="1" stdDeviation="1" flood-color="#000" flood-opacity="0.8"/></filter></defs><defs data-layer="gradients"></defs><g data-layer="connectors"></g><g data-layer="shapes"></g><g data-layer="overlay"></g>`;
        this.canvas.replaceChildren(this.svg);

        this.renderPalette();
        this.bindEvents();
        this.adapter = this.formatAdapter();
        registerFormatTarget("builder", this.adapter);
        this.render();
        new ResizeObserver(() => this.fitView()).observe(this.canvas);
        if (saved !== null && saved.diagram.shapes.length > 0) {
            this.announce(`Restored a saved diagram with ${saved.diagram.shapes.length} shapes.`);
        }
    }

    get diagram() {
        return this.history.present;
    }

    dispose() {
        unregisterFormatTarget("builder", this.adapter);
        this.controller.abort();
        window.clearTimeout(this.saveTimer);
        this.saveNow();
    }

    // ---- state changes -------------------------------------------------------------------------

    apply(next, message) {
        this.history = commit(this.history, next);
        this.afterChange(message);
    }

    afterChange(message) {
        const ids = new Set([...this.diagram.shapes.map(shape => shape.id), ...this.diagram.connectors.map(item => item.id)]);
        this.selection = this.selection.filter(id => ids.has(id));
        this.render();
        this.scheduleSave();
        if (message) {
            this.announce(message);
        }
    }

    undo() {
        if (this.history.past.length === 0) {
            this.announce("Nothing to undo.");
            return;
        }
        this.history = undo(this.history);
        this.afterChange("Undone.");
    }

    redo() {
        if (this.history.future.length === 0) {
            this.announce("Nothing to redo.");
            return;
        }
        this.history = redo(this.history);
        this.afterChange("Redone.");
    }

    scheduleSave() {
        window.clearTimeout(this.saveTimer);
        this.saveTimer = window.setTimeout(() => this.saveNow(), 400);
    }

    saveNow() {
        try {
            window.localStorage.setItem(this.storageKey, serializeDiagram(this.diagram, this.scratchpad));
            return true;
        } catch {
            return false;
        }
    }

    announce(message) {
        if (this.status !== null) {
            this.status.textContent = message;
        }
    }

    // Selection only toggles classes on the existing elements; re-creating them would break a
    // double-click that starts by selecting a shape.
    select(ids) {
        this.selection = [...new Set(ids)];
        const selected = new Set(this.selection);
        for (const element of this.svg.querySelectorAll("[data-builder-id]")) {
            const kind = element.classList.contains("builder-shape") ? "builder-shape" : "builder-connector";
            element.classList.toggle(`${kind}--selected`, selected.has(element.dataset.builderId));
        }
        this.updateToolbar();
        notifyFormatChanged();
    }

    /** What the Format panel edits while Build Flowchart is open. */
    formatAdapter() {
        const shapesOnly = () => this.selection.filter(id => id.startsWith("s"));
        return {
            label: "Build Flowchart",
            selectHint: "Click a shape or connector; Shift+click adds more.",
            resetLabel: "Reset style",
            supportsPerimeter: true,
            selection: () => this.selection.map(id => {
                const shape = this.diagram.shapes.find(item => item.id === id);
                if (shape !== undefined) {
                    return { id, type: "shape", name: describeShape(shape), style: { ...shape.style }, box: { x: shape.x, y: shape.y, width: shape.width, height: shape.height, rotation: shape.rotation ?? 0 } };
                }
                const connector = this.diagram.connectors.find(item => item.id === id);
                return connector === undefined ? null : { id, type: "connector", name: connectorCatalog.find(entry => entry.kind === connector.kind).label, style: { ...connector.style } };
            }).filter(Boolean),
            applyStyle: patch => this.apply(setStyles(this.diagram, this.selection, patch)),
            replaceStyle: style => this.apply(replaceStyles(this.diagram, this.selection, style)),
            resetStyle: keys => this.apply(clearStyles(this.diagram, this.selection, keys), keys === null ? "Style reset." : "Formatting cleared."),
            setDefaultStyle: style => this.apply(setDefaultStyle(this.diagram, style), "New shapes will use this style."),
            setGeometry: patch => this.apply(setGeometry(this.diagram, shapesOnly(), patch)),
            align: mode => this.apply(alignShapes(this.diagram, shapesOnly(), mode), `Aligned ${mode}.`),
            distribute: axis => this.apply(distributeShapes(this.diagram, shapesOnly(), axis), `Distributed ${axis}ly.`),
            order: where => this.apply(reorderShapes(this.diagram, shapesOnly(), where), where === "front" ? "Brought to front." : "Sent to back."),
            undo: () => this.undo(),
            redo: () => this.redo()
        };
    }

    // ---- palette --------------------------------------------------------------------------------

    renderPalette() {
        const query = this.root.querySelector("[data-builder-search]")?.value ?? "";
        const shapes = this.root.querySelector("[data-builder-shapes]");
        const connectors = this.root.querySelector("[data-builder-connectors]");
        const scratch = this.root.querySelector("[data-builder-scratchpad-items]");
        const visibleShapes = filterCatalog(shapeCatalog, query);
        const visibleConnectors = filterCatalog(connectorCatalog, query);
        shapes?.replaceChildren(...visibleShapes.map(item => paletteButton(item, `shape:${item.kind}`, `Add ${item.label}`)));
        connectors?.replaceChildren(...visibleConnectors.map(item => {
            const button = connectorButton(item);
            button.setAttribute("aria-pressed", String(item.kind === this.connectorKind));
            return button;
        }));
        scratch?.replaceChildren(...this.scratchpad.map((item, index) => {
            const wrapper = document.createElement("div");
            wrapper.className = "builder-scratch-item";
            const label = item.text ? `${catalogItem(item.kind)?.label}: ${item.text}` : catalogItem(item.kind)?.label;
            const button = paletteButton({ ...catalogItem(item.kind), width: item.width, height: item.height }, `scratch:${index}`, `Add ${label} from the scratchpad`);
            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "builder-scratch-item__remove";
            remove.dataset.builderRemoveScratch = String(index);
            remove.setAttribute("aria-label", `Remove ${label} from the scratchpad`);
            remove.title = "Remove from scratchpad";
            remove.textContent = "×";
            wrapper.append(button, remove);
            return wrapper;
        }));
        const empty = this.root.querySelector("[data-builder-empty-search]");
        if (empty !== null) {
            empty.hidden = visibleShapes.length + visibleConnectors.length > 0;
        }
        this.root.querySelector("[data-builder-scratchpad]")?.classList.toggle("builder-scratchpad--filled", this.scratchpad.length > 0);
    }

    templateFor(source) {
        const [type, value] = source.split(":");
        if (type === "shape") {
            const item = catalogItem(value);
            return item === null ? null : { kind: item.kind };
        }
        if (type === "scratch") {
            return this.scratchpad[Number(value)] ?? null;
        }
        return null;
    }

    addFromTemplate(template, cx, cy) {
        const { diagram, id } = addShape(this.diagram, template.kind, cx, cy, template.width === undefined ? null : template);
        if (id === null) {
            this.announce(`The diagram already has the maximum of ${builderLimits.shapes} shapes.`);
            return null;
        }
        this.selection = [id];
        this.apply(diagram, `Added ${catalogItem(template.kind).label}.`);
        return id;
    }

    // Click (or Enter) on a palette item places it at the centre of the view, stepping aside if a
    // shape is already centred there, so repeated clicks do not stack shapes exactly.
    addAtViewCenter(template) {
        const rect = this.canvas.getBoundingClientRect();
        let center = this.toDiagram(rect.left + rect.width / 2, rect.top + rect.height / 2);
        for (let attempt = 0; attempt < 20; attempt++) {
            const taken = this.diagram.shapes.some(shape => Math.abs(shape.x + shape.width / 2 - center.x) < 1 && Math.abs(shape.y + shape.height / 2 - center.y) < 1);
            if (!taken) {
                break;
            }
            center = { x: center.x + 24, y: center.y + 24 };
        }
        return this.addFromTemplate(template, center.x, center.y);
    }

    addToScratchpad(template) {
        if (this.scratchpad.length >= builderLimits.scratchpad) {
            this.announce(`The scratchpad holds at most ${builderLimits.scratchpad} items.`);
            return;
        }
        const item = parseTemplate({ kind: template.kind, width: template.width ?? catalogItem(template.kind).width, height: template.height ?? catalogItem(template.kind).height, text: template.text ?? catalogItem(template.kind).text ?? "", style: template.style ?? {} });
        if (item !== null) {
            this.scratchpad = [...this.scratchpad, item];
            this.renderPalette();
            this.scheduleSave();
            this.announce(`Added ${catalogItem(item.kind).label} to the scratchpad.`);
        }
    }

    // ---- rendering -----------------------------------------------------------------------------

    render() {
        const focusedId = document.activeElement?.closest?.("[data-builder-id]")?.dataset.builderId ?? null;
        const selected = new Set(this.selection);
        const routes = routeConnectors(this.diagram);
        const shapesById = new Map(this.diagram.shapes.map(shape => [shape.id, shape]));

        const gradients = this.svg.querySelector('[data-layer="gradients"]');
        gradients.replaceChildren();
        const connectorLayer = this.svg.querySelector('[data-layer="connectors"]');
        connectorLayer.replaceChildren(...this.diagram.connectors.filter(item => routes.has(item.id)).map(item => {
            const route = routes.get(item.id);
            const group = svgElement("g", { class: "builder-connector", "data-builder-id": item.id, tabindex: "0", role: "button" });
            group.classList.toggle("builder-connector--selected", selected.has(item.id));
            const from = shapesById.get(item.from);
            const to = shapesById.get(item.to);
            group.setAttribute("aria-label", `${connectorCatalog.find(entry => entry.kind === item.kind).label} from ${describeShape(from)} to ${describeShape(to)}${item.label ? `, labelled ${item.label}` : ""}`);
            const sparse = item.style ?? {};
            const s = effectiveStyle(sparse);
            const width = "lineWidth" in sparse ? ptToPx(s.lineWidth) : 1.75;
            const lineColor = "lineColor" in sparse ? s.lineColor : style.connector;
            group.setAttribute("opacity", String(s.opacity / 100));
            group.append(
                svgElement("path", { class: "builder-connector__hit", d: route.d, fill: "none", stroke: "transparent", "stroke-width": "14" }),
                svgElement("path", {
                    class: "builder-connector__line",
                    d: route.d,
                    fill: "none",
                    stroke: !s.lineEnabled ? "none" : lineColor,
                    "stroke-width": width,
                    "stroke-dasharray": "lineStyle" in sparse ? dashArray(s.lineStyle, width) : item.kind === "dashed" ? "6 4" : null,
                    filter: s.shadow ? "url(#builder-shadow)" : null,
                    "marker-end": !s.lineEnabled ? null : `url(#${arrowheadFor(gradients, lineColor)})`
                }));
            if (item.label) {
                const fontSize = "fontSize" in sparse ? s.fontSize : 13;
                group.append(textElement(item.label.split("\n"), route.label.x, route.label.y, fontSize, Math.round(fontSize * 1.25), Boolean(s.bold), "builder-connector__label", sparse, !s.textBackgroundEnabled));
            }
            return group;
        }));

        const shapeLayer = this.svg.querySelector('[data-layer="shapes"]');
        shapeLayer.replaceChildren(...this.diagram.shapes.map(shape => {
            const outline = shapeOutline(shape.kind, shape.width, shape.height);
            const sparse = shape.style ?? {};
            const s = effectiveStyle(sparse);
            const rotation = shape.rotation ? ` rotate(${shape.rotation}, ${round(shape.width / 2)}, ${round(shape.height / 2)})` : "";
            const group = svgElement("g", {
                class: "builder-shape",
                "data-builder-id": shape.id,
                transform: `translate(${shape.x}, ${shape.y})${rotation}`,
                opacity: s.opacity === 100 ? null : String(s.opacity / 100),
                tabindex: "0",
                role: "button",
                "aria-label": `${describeShape(shape)}. Double-click or press Enter to edit text.`
            });
            group.classList.toggle("builder-shape--selected", selected.has(shape.id));
            const body = shapeBody(shape, outline, sparse, s, gradients);
            group.append(svgElement("path", { class: "builder-shape__body", ...body }));
            for (const detail of outline.details) {
                group.append(svgElement("path", { class: "builder-shape__detail", d: detail, fill: "none", stroke: shape.kind === "actor" ? ("lineColor" in sparse ? s.lineColor : style.text) : body.stroke === "none" ? style.stroke : body.stroke, "stroke-width": body["stroke-width"] }));
            }
            const text = textLayout(shape);
            if (shape.text) {
                group.append(textElement(text.lines, text.x, text.firstY, text.fontSize, text.lineHeight, text.bold, "builder-shape__text", sparse, false, text.anchor));
            }
            for (const side of ["top", "right", "bottom", "left"]) {
                const port = portOf({ cx: shape.width / 2, cy: shape.height / 2, width: shape.width, height: shape.height }, side);
                group.append(svgElement("circle", { class: "builder-handle", "data-builder-handle": side, cx: port.x, cy: port.y, r: "5" }));
            }
            return group;
        }));

        this.drawTextBoxes();
        if (focusedId !== null) {
            this.svg.querySelector(`[data-builder-id="${CSS.escape(focusedId)}"]`)?.focus({ preventScroll: true });
        }
        this.renderOutline(routes);
        this.updateToolbar();
        this.fitView();
        notifyFormatChanged();
    }

    // Text tab Background and Border colour: a rectangle measured around the drawn text.
    drawTextBoxes() {
        const items = [...this.diagram.shapes, ...this.diagram.connectors];
        for (const item of items) {
            const s = effectiveStyle(item.style);
            if (!s.textBackgroundEnabled && !s.borderColorEnabled) {
                continue;
            }
            const text = this.svg.querySelector(`[data-builder-id="${CSS.escape(item.id)}"] text`);
            if (text === null) {
                continue;
            }
            const box = text.getBBox();
            text.before(svgElement("rect", {
                class: "builder-text-box",
                x: round(box.x - 4),
                y: round(box.y - 2),
                width: round(box.width + 8),
                height: round(box.height + 4),
                rx: "2",
                fill: s.textBackgroundEnabled ? s.textBackground : "none",
                stroke: s.borderColorEnabled ? s.borderColor : "none",
                "stroke-width": s.borderColorEnabled ? "1" : null
            }));
        }
    }

    renderOutline() {
        if (this.outline === null) {
            return;
        }
        const shapes = new Map(this.diagram.shapes.map(shape => [shape.id, shape]));
        const items = [
            ...this.diagram.shapes.map(shape => `${describeShape(shape)} at x ${Math.round(shape.x)}, y ${Math.round(shape.y)}`),
            ...this.diagram.connectors.map(item => `${describeShape(shapes.get(item.from))} → ${describeShape(shapes.get(item.to))}${item.label ? ` (${item.label})` : ""}`)
        ];
        this.outline.replaceChildren(...(items.length === 0 ? ["The diagram is empty. Add a shape from the palette."] : items).map(text => {
            const li = document.createElement("li");
            li.textContent = text;
            return li;
        }));
    }

    updateToolbar() {
        const set = (command, disabled) => {
            const button = this.root.querySelector(`[data-builder-command="${command}"]`);
            if (button !== null) {
                button.disabled = disabled;
            }
        };
        set("undo", this.history.past.length === 0);
        set("redo", this.history.future.length === 0);
        set("delete", this.selection.length === 0);
        set("connect", this.selection.filter(id => id.startsWith("s")).length !== 2);
        set("clear", this.diagram.shapes.length === 0);
        if (this.zoomOutput !== null) {
            this.zoomOutput.textContent = `${Math.round(this.zoom * 100)}%`;
        }
    }

    /**
     * Infinite canvas: the SVG covers the visible area and every shape (plus room to keep dragging).
     * Shapes left of or above the origin give negative coordinates; the scroll position is corrected
     * so nothing jumps on screen.
     */
    fitView() {
        const viewWidth = Math.max(1, this.canvas.clientWidth / this.zoom);
        const viewHeight = Math.max(1, this.canvas.clientHeight / this.zoom);
        const base = { x: 0, y: 0, width: viewWidth, height: viewHeight };
        const next = growBounds(base, diagramBounds(this.diagram), 160);
        const current = this.svg.viewBox.baseVal;
        if (current !== null && current.width > 0 && current.x === next.x && current.y === next.y && current.width === next.width && current.height === next.height) {
            return;
        }
        const before = current !== null && current.width > 0 ? this.svg.getScreenCTM() : null;
        this.svg.setAttribute("viewBox", `${next.x} ${next.y} ${next.width} ${next.height}`);
        this.svg.style.width = `${Math.round(next.width * this.zoom)}px`;
        this.svg.style.height = `${Math.round(next.height * this.zoom)}px`;
        const after = this.svg.getScreenCTM();
        if (before !== null && after !== null) {
            this.canvas.scrollLeft += after.e - before.e;
            this.canvas.scrollTop += after.f - before.f;
        }
    }

    setZoom(value) {
        const rect = this.canvas.getBoundingClientRect();
        const center = this.toDiagram(rect.left + rect.width / 2, rect.top + rect.height / 2);
        this.zoom = Math.min(maximumZoom, Math.max(minimumZoom, Math.round(value * 100) / 100));
        this.svg.removeAttribute("viewBox");
        this.fitView();
        // Keep the same diagram point in the middle of the view.
        const after = this.toScreen(center.x, center.y);
        this.canvas.scrollLeft += after.x - (rect.left + rect.width / 2);
        this.canvas.scrollTop += after.y - (rect.top + rect.height / 2);
        this.updateToolbar();
        this.announce(`Zoom ${Math.round(this.zoom * 100)}%.`);
    }

    toDiagram(clientX, clientY) {
        const matrix = this.svg.getScreenCTM();
        const point = this.svg.createSVGPoint();
        point.x = clientX;
        point.y = clientY;
        const local = matrix === null ? point : point.matrixTransform(matrix.inverse());
        return { x: local.x, y: local.y };
    }

    toScreen(x, y) {
        const matrix = this.svg.getScreenCTM();
        const point = this.svg.createSVGPoint();
        point.x = x;
        point.y = y;
        const screen = matrix === null ? point : point.matrixTransform(matrix);
        return { x: screen.x, y: screen.y };
    }

    // ---- events --------------------------------------------------------------------------------

    bindEvents() {
        const signal = this.controller.signal;
        const on = (target, type, handler, options = {}) => target?.addEventListener(type, handler, { ...options, signal });

        on(this.root.querySelector("[data-builder-search]"), "input", () => this.renderPalette());

        on(this.root, "click", event => {
            const command = event.target.closest?.("[data-builder-command]");
            if (command !== null && command !== undefined && this.root.contains(command)) {
                command.closest("details")?.removeAttribute("open");
                this.runCommand(command.dataset.builderCommand);
                return;
            }
            const remove = event.target.closest?.("[data-builder-remove-scratch]");
            if (remove) {
                this.scratchpad = this.scratchpad.filter((_, index) => index !== Number(remove.dataset.builderRemoveScratch));
                this.renderPalette();
                this.scheduleSave();
                this.announce("Removed from the scratchpad.");
                return;
            }
            const connector = event.target.closest?.("[data-builder-connector-kind]");
            if (connector) {
                this.useConnectorKind(connector.dataset.builderConnectorKind);
                return;
            }
            const item = event.target.closest?.("[data-builder-source]");
            if (item) {
                const template = this.templateFor(item.dataset.builderSource);
                if (template !== null) {
                    this.addAtViewCenter(template);
                }
            }
        });

        // Palette → canvas or scratchpad (HTML drag and drop).
        on(this.root, "dragstart", event => {
            const item = event.target.closest?.("[data-builder-source]");
            if (item) {
                event.dataTransfer.setData("application/x-codelogic-shape", item.dataset.builderSource);
                event.dataTransfer.effectAllowed = "copy";
            }
        });
        const accepts = event => event.dataTransfer?.types.includes("application/x-codelogic-shape");
        on(this.canvas, "dragover", event => {
            if (accepts(event)) {
                event.preventDefault();
                event.dataTransfer.dropEffect = "copy";
            }
        });
        on(this.canvas, "drop", event => {
            if (!accepts(event)) {
                return;
            }
            event.preventDefault();
            const template = this.templateFor(event.dataTransfer.getData("application/x-codelogic-shape"));
            if (template !== null) {
                const point = this.toDiagram(event.clientX, event.clientY);
                this.addFromTemplate(template, point.x, point.y);
            }
        });
        const scratchpad = this.root.querySelector("[data-builder-scratchpad]");
        on(scratchpad, "dragover", event => {
            if (accepts(event)) {
                event.preventDefault();
                scratchpad.classList.add("builder-scratchpad--over");
            }
        });
        on(scratchpad, "dragleave", () => scratchpad.classList.remove("builder-scratchpad--over"));
        on(scratchpad, "drop", event => {
            scratchpad.classList.remove("builder-scratchpad--over");
            if (!accepts(event)) {
                return;
            }
            event.preventDefault();
            const template = this.templateFor(event.dataTransfer.getData("application/x-codelogic-shape"));
            if (template !== null) {
                this.addToScratchpad(template);
            }
        });

        on(this.svg, "pointerdown", event => this.onPointerDown(event));
        on(this.svg, "pointermove", event => this.onPointerMove(event));
        on(this.svg, "pointerup", event => this.onPointerUp(event));
        on(this.svg, "pointercancel", () => this.cancelGesture());
        on(window, "pointerup", () => { this.pointerActive = false; });
        on(this.svg, "dblclick", event => {
            const target = event.target.closest?.("[data-builder-id]");
            if (target) {
                event.preventDefault();
                this.startEditing(target.dataset.builderId);
            }
        });
        on(this.svg, "focusin", event => {
            const target = event.target.closest?.("[data-builder-id]");
            // Keyboard focus selects the shape; a click has already updated the selection itself.
            if (target && !this.pointerActive && !this.selection.includes(target.dataset.builderId)) {
                this.select([target.dataset.builderId]);
            }
        });
        on(this.canvas, "wheel", event => {
            if (event.ctrlKey) {
                event.preventDefault();
                this.setZoom(this.zoom + (event.deltaY < 0 ? 0.1 : -0.1));
            }
        }, { passive: false });
        on(this.root, "keydown", event => this.onKeyDown(event));
    }

    useConnectorKind(kind) {
        if (!connectorKinds.has(kind)) {
            return;
        }
        this.connectorKind = kind;
        const label = connectorCatalog.find(item => item.kind === kind).label;
        const selectedConnectors = this.selection.filter(id => id.startsWith("c"));
        if (selectedConnectors.length > 0) {
            this.apply(setConnectorKind(this.diagram, selectedConnectors, kind), `Changed ${selectedConnectors.length} connector(s) to ${label}.`);
        } else {
            this.announce(`${label} selected. Drag from a shape's handle to another shape to connect them.`);
        }
        this.renderPalette();
    }

    runCommand(command) {
        switch (command) {
            case "undo": this.undo(); break;
            case "redo": this.redo(); break;
            case "delete": this.deleteSelection(); break;
            case "connect": this.connectSelection(); break;
            case "zoom-in": this.setZoom(this.zoom + 0.1); break;
            case "zoom-out": this.setZoom(this.zoom - 0.1); break;
            case "zoom-reset": this.setZoom(1); break;
            case "save": this.announce(this.saveNow() ? "Diagram saved in this browser." : "This browser blocked saving the diagram."); break;
            case "export-json": download(new Blob([serializeDiagram(this.diagram)], { type: "application/json" }), "codelogic-diagram.json"); this.announce("Exported the diagram as JSON."); break;
            case "export-svg": download(new Blob([this.exportSvg()], { type: "image/svg+xml" }), "codelogic-diagram.svg"); this.announce("Exported the diagram as SVG."); break;
            case "export-png": this.exportPng(); break;
            case "clear":
                if (window.confirm("Remove every shape and connector from the diagram? You can undo this.")) {
                    this.selection = [];
                    this.apply(createDiagram(), "Cleared the diagram. Press Ctrl+Z to undo.");
                }
                break;
        }
    }

    deleteSelection() {
        if (this.selection.length === 0) {
            return;
        }
        const count = this.selection.length;
        const next = deleteItems(this.diagram, this.selection);
        this.selection = [];
        this.apply(next, `Deleted ${count} item(s).`);
    }

    // Keyboard alternative to dragging a handle: select two shapes, then Connect (or press C).
    connectSelection() {
        const shapes = this.selection.filter(id => id.startsWith("s"));
        if (shapes.length !== 2) {
            this.announce("Select exactly two shapes to connect them (Shift+click or Space).");
            return;
        }
        const { diagram, id } = addConnector(this.diagram, shapes[0], shapes[1], this.connectorKind);
        if (id !== null) {
            this.selection = [id];
            this.apply(diagram, "Connected the two shapes.");
        }
    }

    onPointerDown(event) {
        if (event.button !== 0 || this.editing !== null) {
            return;
        }
        this.pointerActive = true;
        const handle = event.target.closest?.("[data-builder-handle]");
        const item = event.target.closest?.("[data-builder-id]");
        const start = this.toDiagram(event.clientX, event.clientY);
        const additive = event.shiftKey || event.ctrlKey || event.metaKey;

        if (handle && item) {
            event.preventDefault();
            this.gesture = { type: "connect", from: item.dataset.builderId, start, client: { x: event.clientX, y: event.clientY }, pointerId: event.pointerId, moved: false };
            this.svg.setPointerCapture?.(event.pointerId);
            return;
        }
        if (item) {
            const id = item.dataset.builderId;
            if (additive) {
                this.select(this.selection.includes(id) ? this.selection.filter(other => other !== id) : [...this.selection, id]);
            } else if (!this.selection.includes(id)) {
                this.select([id]);
            }
            const shapeIds = this.selection.filter(other => other.startsWith("s"));
            if (id.startsWith("s") && shapeIds.includes(id)) {
                this.gesture = { type: "move", ids: shapeIds, start, from: this.diagram, client: { x: event.clientX, y: event.clientY }, origin: { x: event.clientX, y: event.clientY }, pointerId: event.pointerId, moved: false };
            }
            return;
        }
        // Empty canvas: clear the selection and pan.
        if (!additive && this.selection.length > 0) {
            this.select([]);
        }
        this.gesture = { type: "pan", client: { x: event.clientX, y: event.clientY }, left: this.canvas.scrollLeft, top: this.canvas.scrollTop, pointerId: event.pointerId };
        this.svg.setPointerCapture?.(event.pointerId);
        this.canvas.classList.add("builder-canvas--panning");
    }

    onPointerMove(event) {
        const gesture = this.gesture;
        if (gesture === undefined) {
            return;
        }
        if (gesture.type === "pan") {
            this.canvas.scrollLeft = gesture.left - (event.clientX - gesture.client.x);
            this.canvas.scrollTop = gesture.top - (event.clientY - gesture.client.y);
            return;
        }
        gesture.client = { x: event.clientX, y: event.clientY };
        if (!gesture.moved) {
            const origin = gesture.origin ?? gesture.client;
            if (gesture.type === "move" && !isDrag(event.clientX - origin.x, event.clientY - origin.y)) {
                return;
            }
            gesture.moved = true;
            if (gesture.type === "move") {
                this.svg.setPointerCapture?.(gesture.pointerId);
                this.canvas.classList.add("builder-canvas--dragging");
            }
            gesture.frame = window.requestAnimationFrame(() => this.autoScroll());
        }
        event.preventDefault();
        this.followPointer();
    }

    followPointer() {
        const gesture = this.gesture;
        const point = this.toDiagram(gesture.client.x, gesture.client.y);
        if (gesture.type === "move") {
            this.history = { ...this.history, present: moveShapes(gesture.from, gesture.ids, point.x - gesture.start.x, point.y - gesture.start.y) };
            this.render();
        } else if (gesture.type === "connect") {
            const shape = this.diagram.shapes.find(item => item.id === gesture.from);
            const source = shapeBox(shape);
            const overlay = this.svg.querySelector('[data-layer="overlay"]');
            const start = portOf(source, chooseSides(source, { cx: point.x, cy: point.y, width: 0, height: 0 }).exit);
            overlay.replaceChildren(svgElement("path", { class: "builder-connector__preview", d: pathData([start, point]), fill: "none", stroke: style.stroke, "stroke-width": "1.75", "stroke-dasharray": "4 3", "marker-end": "url(#builder-arrowhead)" }));
        }
    }

    // While a drag is near or past an edge the canvas keeps scrolling (and growing).
    autoScroll() {
        const gesture = this.gesture;
        if (gesture === undefined || !gesture.moved) {
            return;
        }
        const view = this.canvas.getBoundingClientRect();
        const left = edgeScrollSpeed(gesture.client.x, view.left, view.right);
        const top = edgeScrollSpeed(gesture.client.y, view.top, view.bottom);
        if (left !== 0 || top !== 0) {
            this.canvas.scrollBy({ left, top, behavior: "instant" });
            this.followPointer();
        }
        gesture.frame = window.requestAnimationFrame(() => this.autoScroll());
    }

    onPointerUp(event) {
        this.pointerActive = false;
        const gesture = this.gesture;
        this.gesture = undefined;
        this.canvas.classList.remove("builder-canvas--panning", "builder-canvas--dragging");
        if (gesture === undefined) {
            return;
        }
        window.cancelAnimationFrame(gesture.frame ?? 0);
        if (gesture.type === "move" && gesture.moved) {
            const moved = this.history.present;
            this.history = { ...this.history, present: gesture.from };
            // Dropping shapes on the scratchpad keeps a reusable copy and leaves the diagram as it was.
            const dropTarget = document.elementFromPoint(event.clientX, event.clientY);
            if (dropTarget?.closest?.("[data-builder-scratchpad]")) {
                for (const id of gesture.ids) {
                    const shape = gesture.from.shapes.find(item => item.id === id);
                    this.addToScratchpad(shape);
                }
                this.render();
                return;
            }
            this.apply(moved, `Moved ${gesture.ids.length} shape(s).`);
        } else if (gesture.type === "connect") {
            this.svg.querySelector('[data-layer="overlay"]').replaceChildren();
            const target = document.elementFromPoint(event.clientX, event.clientY)?.closest?.(".builder-shape");
            const to = target?.dataset.builderId;
            if (to === undefined || to === gesture.from) {
                this.announce("Drop the connector on another shape to connect them.");
                return;
            }
            const { diagram, id } = addConnector(this.diagram, gesture.from, to, this.connectorKind);
            if (id !== null) {
                this.selection = [id];
                this.apply(diagram, "Connected the two shapes.");
            }
        }
    }

    cancelGesture() {
        this.pointerActive = false;
        const gesture = this.gesture;
        this.gesture = undefined;
        this.canvas.classList.remove("builder-canvas--panning", "builder-canvas--dragging");
        if (gesture?.type === "move" && gesture.moved) {
            this.history = { ...this.history, present: gesture.from };
        }
        window.cancelAnimationFrame(gesture?.frame ?? 0);
        this.svg.querySelector('[data-layer="overlay"]').replaceChildren();
        this.render();
    }

    onKeyDown(event) {
        if (event.target.closest?.("input, textarea, select")) {
            if (event.key === "Escape" && event.target.matches("[data-builder-search]") && event.target.value !== "") {
                event.preventDefault();
                event.target.value = "";
                this.renderPalette();
            }
            return;
        }
        const ctrl = event.ctrlKey || event.metaKey;
        const key = event.key.toLowerCase();
        if (event.key === "/" && !ctrl) {
            event.preventDefault();
            this.root.querySelector("[data-builder-search]")?.focus();
            return;
        }
        // Ctrl+C / Ctrl+V copy and paste a style; Ctrl+Z / Ctrl+Y undo and redo.
        if (handleFormatShortcut(event, this.adapter)) {
            return;
        }
        if (ctrl && key === "s") {
            event.preventDefault();
            this.runCommand("save");
            return;
        }
        // The remaining keys act on the canvas only, so palette buttons keep their own keys.
        if (!event.target.closest?.("[data-builder-canvas]")) {
            return;
        }
        const focused = event.target.closest?.("[data-builder-id]")?.dataset.builderId;
        if (ctrl && key === "a") {
            event.preventDefault();
            this.select(this.diagram.shapes.map(shape => shape.id));
            this.announce(`Selected ${this.diagram.shapes.length} shapes.`);
        } else if (event.key === "Delete" || event.key === "Backspace") {
            event.preventDefault();
            this.deleteSelection();
        } else if ((event.key === "Enter" || event.key === "F2") && this.selection.length === 1) {
            event.preventDefault();
            this.startEditing(this.selection[0]);
        } else if (event.key === " " && focused !== undefined) {
            event.preventDefault();
            this.select(this.selection.includes(focused) ? this.selection.filter(id => id !== focused) : [...this.selection, focused]);
        } else if (key === "c" && !ctrl) {
            event.preventDefault();
            this.connectSelection();
        } else if (event.key === "Escape" && this.selection.length > 0) {
            event.preventDefault();
            this.select([]);
            this.announce("Selection cleared.");
        } else if (event.key.startsWith("Arrow") && !ctrl) {
            const shapes = this.selection.filter(id => id.startsWith("s"));
            if (shapes.length === 0) {
                return;
            }
            event.preventDefault();
            const step = event.shiftKey ? 50 : 10;
            const dx = event.key === "ArrowLeft" ? -step : event.key === "ArrowRight" ? step : 0;
            const dy = event.key === "ArrowUp" ? -step : event.key === "ArrowDown" ? step : 0;
            this.apply(moveShapes(this.diagram, shapes, dx, dy));
            this.revealSelection();
        }
    }

    revealSelection() {
        const element = this.svg.querySelector(`[data-builder-id="${CSS.escape(this.selection[0] ?? "")}"]`);
        if (element === null) {
            return;
        }
        const margin = 24;
        const view = this.canvas.getBoundingClientRect();
        const box = element.getBoundingClientRect();
        const left = box.left < view.left + margin ? box.left - view.left - margin : box.right > view.right - margin ? box.right - view.right + margin : 0;
        const top = box.top < view.top + margin ? box.top - view.top - margin : box.bottom > view.bottom - margin ? box.bottom - view.bottom + margin : 0;
        this.canvas.scrollBy({ left, top, behavior: "instant" });
    }

    // ---- inline text editing -------------------------------------------------------------------

    // Enter adds a line break; Ctrl+Enter or clicking outside saves; Esc cancels.
    startEditing(id) {
        const shape = this.diagram.shapes.find(item => item.id === id);
        const connector = this.diagram.connectors.find(item => item.id === id);
        if (shape === undefined && connector === undefined) {
            return;
        }
        this.finishEditing(true);
        const element = this.svg.querySelector(`[data-builder-id="${CSS.escape(id)}"]`);
        const anchor = shape !== undefined
            ? element.querySelector(".builder-shape__body").getBoundingClientRect()
            : (element.querySelector(".builder-connector__label") ?? element).getBoundingClientRect();
        const host = this.canvas.getBoundingClientRect();
        const editor = document.createElement("textarea");
        editor.className = "builder-text-editor";
        editor.value = shape?.text ?? connector.label;
        editor.maxLength = builderLimits.text;
        editor.setAttribute("aria-label", shape !== undefined ? `Text of ${describeShape(shape)}. Enter adds a line, Ctrl+Enter saves, Esc cancels.` : "Connector label. Enter adds a line, Ctrl+Enter saves, Esc cancels.");
        const width = Math.max(120, anchor.width);
        const height = Math.max(44, shape !== undefined ? anchor.height : 44);
        editor.style.left = `${anchor.left + anchor.width / 2 - width / 2 - host.left + this.canvas.scrollLeft}px`;
        editor.style.top = `${anchor.top + anchor.height / 2 - height / 2 - host.top + this.canvas.scrollTop}px`;
        editor.style.width = `${width}px`;
        editor.style.height = `${height}px`;
        this.canvas.append(editor);
        // Clicking anywhere outside saves. A pointer listener (not only blur) keeps this reliable
        // even when the browser window itself is not focused.
        const outside = new AbortController();
        document.addEventListener("pointerdown", event => {
            if (event.target !== editor) {
                this.finishEditing(true);
            }
        }, { capture: true, signal: outside.signal });
        this.editing = { id, editor, outside };
        editor.addEventListener("keydown", event => {
            event.stopPropagation();
            if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
                event.preventDefault();
                this.finishEditing(true);
            } else if (event.key === "Escape") {
                event.preventDefault();
                this.finishEditing(false);
            }
        });
        editor.addEventListener("blur", () => this.finishEditing(true));
        editor.focus();
        editor.select();
    }

    finishEditing(save) {
        const editing = this.editing;
        if (editing === null) {
            return;
        }
        this.editing = null;
        editing.outside.abort();
        const value = editing.editor.value;
        editing.editor.remove();
        if (save) {
            const isShape = editing.id.startsWith("s");
            const next = isShape ? setShapeText(this.diagram, editing.id, value) : setConnectorLabel(this.diagram, editing.id, value);
            const current = isShape ? this.diagram.shapes.find(item => item.id === editing.id)?.text : this.diagram.connectors.find(item => item.id === editing.id)?.label;
            if (current !== value) {
                this.apply(next, isShape ? "Text updated." : "Connector label updated.");
            }
        }
        this.svg.querySelector(`[data-builder-id="${CSS.escape(editing.id)}"]`)?.focus({ preventScroll: true });
    }

    // ---- export --------------------------------------------------------------------------------

    /** A standalone SVG of the diagram: no handles or selection, with its own background. */
    exportSvg() {
        // Measured from what is drawn, so connectors routed around shapes and their labels fit too.
        const bounds = this.diagram.shapes.length === 0 ? { x: 0, y: 0, width: 200, height: 100 } : unionBox(
            ["connectors", "shapes"].map(layer => this.svg.querySelector(`[data-layer="${layer}"]`).getBBox()).filter(box => box.width > 0 || box.height > 0));
        const padding = 40;
        const box = { x: bounds.x - padding, y: bounds.y - padding, width: bounds.width + padding * 2, height: bounds.height + padding * 2 };
        const clone = this.svg.cloneNode(true);
        clone.querySelectorAll(".builder-handle, .builder-connector__hit, [data-layer='overlay']").forEach(element => element.remove());
        clone.querySelectorAll("[tabindex], [role], [aria-label], [data-builder-id]").forEach(element => {
            for (const attribute of ["tabindex", "role", "aria-label", "data-builder-id", "class"]) {
                element.removeAttribute(attribute);
            }
        });
        clone.removeAttribute("style");
        clone.removeAttribute("class");
        clone.setAttribute("xmlns", svgNamespace);
        clone.setAttribute("viewBox", `${box.x} ${box.y} ${box.width} ${box.height}`);
        clone.setAttribute("width", String(Math.ceil(box.width)));
        clone.setAttribute("height", String(Math.ceil(box.height)));
        const background = svgElement("rect", { x: box.x, y: box.y, width: box.width, height: box.height, fill: style.background });
        clone.insertBefore(background, clone.querySelector("defs").nextSibling);
        return `<?xml version="1.0" encoding="UTF-8"?>\n${new XMLSerializer().serializeToString(clone)}`;
    }

    exportPng() {
        const svgText = this.exportSvg();
        const url = URL.createObjectURL(new Blob([svgText], { type: "image/svg+xml" }));
        const image = new Image();
        image.onload = () => {
            const scale = 2;
            const canvas = document.createElement("canvas");
            canvas.width = Math.ceil(image.width * scale);
            canvas.height = Math.ceil(image.height * scale);
            const context = canvas.getContext("2d");
            context.scale(scale, scale);
            context.drawImage(image, 0, 0);
            URL.revokeObjectURL(url);
            canvas.toBlob(blob => {
                if (blob === null) {
                    this.announce("This browser could not create the PNG.");
                    return;
                }
                download(blob, "codelogic-diagram.png");
                this.announce("Exported the diagram as PNG.");
            }, "image/png");
        };
        image.onerror = () => {
            URL.revokeObjectURL(url);
            this.announce("This browser could not create the PNG.");
        };
        image.src = url;
    }
}

function unionBox(boxes) {
    const left = Math.min(...boxes.map(box => box.x));
    const top = Math.min(...boxes.map(box => box.y));
    const right = Math.max(...boxes.map(box => box.x + box.width));
    const bottom = Math.max(...boxes.map(box => box.y + box.height));
    return { x: left, y: top, width: right - left, height: bottom - top };
}

function describeShape(shape) {
    if (shape === undefined) {
        return "a missing shape";
    }
    const label = catalogItem(shape.kind)?.label ?? shape.kind;
    const text = String(shape.text ?? "").replace(/\s+/g, " ").trim();
    return text ? `${label} "${text}"` : label;
}

function svgElement(name, attributes) {
    const element = document.createElementNS(svgNamespace, name);
    for (const [key, value] of Object.entries(attributes)) {
        if (value !== null && value !== undefined) {
            element.setAttribute(key, String(value));
        }
    }
    return element;
}

// The default arrowhead is in <defs>; a coloured line gets a matching arrowhead of its own.
function arrowheadFor(defs, color) {
    if (color === style.connector) {
        return "builder-arrowhead";
    }
    const id = `builder-arrowhead-${color.slice(1)}`;
    if (defs.querySelector(`#${id}`) === null) {
        const marker = svgElement("marker", { id, viewBox: "0 0 10 10", refX: "9", refY: "5", markerWidth: "8", markerHeight: "8", orient: "auto-start-reverse" });
        marker.append(svgElement("path", { d: "M0,0L10,5L0,10Z", fill: color }));
        defs.append(marker);
    }
    return id;
}

// The body of a shape: fill (automatic, solid, gradient, or none), line, shadow, and rounding.
function shapeBody(shape, outline, sparse, s, gradients) {
    const roundable = ["rectangle", "square", "process", "card"].includes(shape.kind);
    const d = s.rounded && roundable ? shapeOutline("rounded", shape.width, shape.height).d : outline.d;
    let fill = outline.outlined ? style.fill : "transparent";
    if (!s.fillEnabled) {
        fill = "none";
    } else if (s.fillMode === "solid") {
        fill = s.fillColor;
        if (s.gradient) {
            const id = `builder-gradient-${shape.id}`;
            const gradient = svgElement("linearGradient", { id, x1: "0", y1: "0", x2: "0", y2: "1" });
            gradient.append(svgElement("stop", { offset: "0", "stop-color": s.fillColor }), svgElement("stop", { offset: "1", "stop-color": s.gradientColor }));
            gradients.append(gradient);
            fill = `url(#${id})`;
        }
    }
    const width = "lineWidth" in sparse ? ptToPx(s.lineWidth) : 1.5;
    const stroke = !s.lineEnabled ? "none" : "lineColor" in sparse ? s.lineColor : outline.outlined ? style.stroke : "none";
    return {
        d,
        // A shape with no fill still needs a hit area, so it stays clickable.
        fill: fill === "none" ? "transparent" : fill,
        stroke,
        "stroke-width": width,
        "stroke-dasharray": "lineStyle" in sparse ? dashArray(s.lineStyle, width) : null,
        filter: s.shadow ? "url(#builder-shadow)" : null
    };
}

// Text is set with textContent only, so diagram text can never become markup.
function textElement(lines, x, firstY, fontSize, lineHeight, bold, className, sparse = {}, withHalo = false, anchor = "middle") {
    const s = effectiveStyle(sparse);
    const text = svgElement("text", {
        class: className,
        x,
        y: firstY,
        "text-anchor": anchor,
        "dominant-baseline": "central",
        "font-family": fontFamilies[s.fontFamily].css ?? style.font,
        "font-size": fontSize,
        "font-weight": bold ? "700" : null,
        "font-style": s.italic ? "italic" : null,
        "text-decoration": textDecoration(s),
        direction: s.direction === "auto" ? null : s.direction,
        "unicode-bidi": s.direction === "auto" ? null : "plaintext",
        opacity: s.textOpacity === 100 ? null : String(s.textOpacity / 100),
        filter: s.textShadow ? "url(#builder-text-shadow)" : null,
        fill: s.fontColorEnabled ? s.fontColor : style.text,
        stroke: withHalo ? style.background : null,
        "stroke-width": withHalo ? "4" : null,
        "paint-order": withHalo ? "stroke" : null
    });
    lines.forEach((line, index) => {
        const span = svgElement("tspan", { x, y: firstY + index * lineHeight });
        span.textContent = line === "" ? " " : line;
        text.append(span);
    });
    return text;
}

function paletteButton(item, source, label) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "builder-palette__item";
    button.draggable = true;
    button.dataset.builderSource = source;
    button.title = `${item.label} — click to add at the centre, or drag onto the canvas`;
    button.setAttribute("aria-label", label);
    button.append(previewIcon(item));
    return button;
}

function connectorButton(item) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "builder-palette__item builder-palette__item--connector";
    button.dataset.builderConnectorKind = item.kind;
    button.title = `${item.label} — used for new connections, or applied to the selected connectors`;
    button.setAttribute("aria-label", `Use ${item.label} for connectors`);
    const icon = svgElement("svg", { viewBox: "0 0 40 28", "aria-hidden": "true", focusable: "false" });
    const d = item.kind === "curved" ? "M4,24C18,24 22,4 34,4" : item.kind === "arrow" ? "M4,24L18,24L18,4L34,4" : "M4,24L34,4";
    icon.append(
        svgElement("defs", {}),
        svgElement("path", { d, fill: "none", stroke: "currentColor", "stroke-width": "2", "stroke-dasharray": item.kind === "dashed" ? "4 3" : null }),
        svgElement("path", { d: "M29,1L37,4L31,9Z", fill: "currentColor" }));
    button.append(icon);
    return button;
}

function previewIcon(item) {
    const scale = Math.min(32 / item.width, 24 / item.height);
    const width = item.width * scale;
    const height = item.height * scale;
    const outline = shapeOutline(item.kind, width, height);
    const icon = svgElement("svg", { viewBox: `-4 -4 ${width + 8} ${height + 8}`, "aria-hidden": "true", focusable: "false" });
    if (item.kind === "text" || item.kind === "heading") {
        const text = svgElement("text", { x: width / 2, y: height / 2, "text-anchor": "middle", "dominant-baseline": "central", "font-size": item.kind === "heading" ? 14 : 11, "font-weight": item.kind === "heading" ? "700" : null, fill: "currentColor" });
        text.textContent = item.kind === "heading" ? "H" : "Text";
        icon.append(text);
        return icon;
    }
    icon.append(svgElement("path", { d: outline.d, fill: outline.outlined ? "rgb(79 143 234 / 0.15)" : "none", stroke: outline.outlined ? "currentColor" : "none", "stroke-width": "1.5" }));
    for (const detail of outline.details) {
        icon.append(svgElement("path", { d: detail, fill: "none", stroke: "currentColor", "stroke-width": "1.5" }));
    }
    return icon;
}

function readStorage(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

function download(blob, fileName) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.append(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
