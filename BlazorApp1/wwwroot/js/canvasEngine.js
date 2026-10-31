/**
 * High-Performance Hybrid 2D Canvas Engine for Blazor Logic Circuit Simulator
 * Supports 1000+ Logic Gates & Manhattan-Routed Wires at 60 FPS:
 *  - Single-Payload Batch Sync & Fast Signal Delta Sync (syncSignals)
 *  - Multi-Layer Rendering (Grid Canvas Layer + Scene Canvas Layer)
 *  - Viewport Frustum AABB Culling for Gates and Wires
 *  - Local 60 FPS Pan, Zoom, Gate Dragging, and Wire Drawing without JSInterop Bottlenecks
 */

const instances = new Map();

const CONSTANTS = {
    GRID_SIZE: 20,
    GATE_WIDTH: 110,
    GATE_HEIGHT: 68,
    BODY_LEFT: 30,
    BODY_RIGHT: 80,
    BODY_HEIGHT: 40,
    BUBBLE_RADIUS: 4,
    PIN_DOT_RADIUS: 4,
    OR_CURVE: 12,
    PIN_PICK_RADIUS_PX: 10,
    SNAP_RADIUS_PX: 18,
    MIN_ZOOM: 0.2,
    MAX_ZOOM: 2.5
};

class CircuitCanvasEngine {
    constructor(viewportId, dotNetRef) {
        this.viewportId = viewportId;
        this.dotNetRef = dotNetRef;

        this.viewportEl = document.getElementById(viewportId);
        this.gridCanvas = document.getElementById(`${viewportId}-grid`);
        this.sceneCanvas = document.getElementById(`${viewportId}-scene`);

        this.gridCtx = this.gridCanvas ? this.gridCanvas.getContext("2d", { alpha: false }) : null;
        this.sceneCtx = this.sceneCanvas ? this.sceneCanvas.getContext("2d") : null;

        this.width = 1200;
        this.height = 700;
        this.dpr = window.devicePixelRatio || 1;

        this.zoom = 1;
        this.panX = 0;
        this.panY = 0;
        this.isPanMode = false;

        this.gates = [];
        this.gateMap = new Map();
        this.wires = [];
        this.selectedGateId = null;
        this.selectedWireIndex = -1;
        this.occupiedInputs = new Set();

        this.gridDirty = true;
        this.sceneDirty = true;
        this.rafId = null;
        this.disposed = false;

        this.isPanning = false;
        this.panStartMouseX = 0;
        this.panStartMouseY = 0;
        this.panMoved = false;

        this.draggingGate = null;
        this.dragOffsetX = 0;
        this.dragOffsetY = 0;
        this.hasMovedGate = false;

        this.wiringSourceGate = null;
        this.isDrawingTempWire = false;
        this.tempWireEndX = 0;
        this.tempWireEndY = 0;
        this.hoverInputGate = null;
        this.hoverInputIndex = -1;
        this.hoverInputValid = false;

        this.activePointers = new Map();
        this.lastPinchDist = 0;

        this.bindEvents();
        this.updateCanvasSize();
        this.scheduleRender();
    }

    bindEvents() {
        if (!this.sceneCanvas) return;

        this.onPointerDown = this.handlePointerDown.bind(this);
        this.onPointerMove = this.handlePointerMove.bind(this);
        this.onPointerUp = this.handlePointerUp.bind(this);
        this.onPointerCancel = this.handlePointerCancel.bind(this);
        this.onWheel = this.handleWheel.bind(this);
        this.onContextMenu = (e) => e.preventDefault();

        this.sceneCanvas.addEventListener("pointerdown", this.onPointerDown);
        this.sceneCanvas.addEventListener("pointermove", this.onPointerMove);
        this.sceneCanvas.addEventListener("pointerup", this.onPointerUp);
        this.sceneCanvas.addEventListener("pointercancel", this.onPointerCancel);
        this.sceneCanvas.addEventListener("wheel", this.onWheel, { passive: false });
        this.sceneCanvas.addEventListener("contextmenu", this.onContextMenu);

        if (window.ResizeObserver && this.viewportEl) {
            this.resizeObserver = new ResizeObserver(() => {
                if (this.disposed) return;
                const changed = this.updateCanvasSize();
                if (changed && this.width > 50 && this.height > 50 && this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnViewportResizedAsync", this.width, this.height, this.panX, this.panY, this.zoom);
                }
            });
            this.resizeObserver.observe(this.viewportEl);
        }
    }

    updateCanvasSize() {
        if (!this.viewportEl || !this.gridCanvas || !this.sceneCanvas) return false;

        const rect = this.viewportEl.getBoundingClientRect();
        let rawW = Math.round(rect.width || this.viewportEl.clientWidth || 0);
        let rawH = Math.round(rect.height || this.viewportEl.clientHeight || 0);

        if ((rawW < 50 || rawH < 50) && this.viewportEl.parentElement) {
            const pRect = this.viewportEl.parentElement.getBoundingClientRect();
            rawW = Math.max(rawW, Math.round(pRect.width || 0));
            rawH = Math.max(rawH, Math.round(pRect.height || 0));
        }

        const w = rawW >= 50 ? rawW : (this.width >= 100 ? this.width : 1100);
        const h = rawH >= 50 ? rawH : (this.height >= 100 ? this.height : 680);
        const dpr = window.devicePixelRatio || 1;

        if (w === this.width && h === this.height && dpr === this.dpr && this.gridCanvas.width > 0) {
            return false;
        }

        this.width = w;
        this.height = h;
        this.dpr = dpr;

        this.gridCanvas.width = Math.round(w * dpr);
        this.gridCanvas.height = Math.round(h * dpr);
        this.sceneCanvas.width = Math.round(w * dpr);
        this.sceneCanvas.height = Math.round(h * dpr);

        this.gridDirty = true;
        this.sceneDirty = true;
        this.scheduleRender();
        return rawW >= 50 && rawH >= 50;
    }

    setScene(payload) {
        if (!payload) return;

        this.updateCanvasSize();

        if (typeof payload.zoom === "number" && payload.zoom > 0) {
            this.zoom = Math.min(CONSTANTS.MAX_ZOOM, Math.max(CONSTANTS.MIN_ZOOM, payload.zoom));
        }
        if (typeof payload.panX === "number") this.panX = payload.panX;
        if (typeof payload.panY === "number") this.panY = payload.panY;
        if (typeof payload.isPanMode === "boolean") this.isPanMode = payload.isPanMode;

        this.selectedGateId = payload.selectedGateId || null;
        this.selectedWireIndex = typeof payload.selectedWireIndex === "number" ? payload.selectedWireIndex : -1;

        this.gates = payload.gates || [];
        this.gateMap.clear();
        for (let i = 0; i < this.gates.length; i++) {
            const g = this.gates[i];
            g.typeUpper = (g.type || "").toUpperCase();
            this.updateGateBounds(g);
            this.gateMap.set(g.id, g);
        }

        this.wires = payload.wires || [];
        this.occupiedInputs.clear();
        for (let i = 0; i < this.wires.length; i++) {
            const w = this.wires[i];
            this.occupiedInputs.add(`${w.targetId}:${w.targetInput}`);
            this.updateWireBounds(w);
        }

        this.updateCursor();
        this.gridDirty = true;
        this.sceneDirty = true;
        this.scheduleRender();
    }

    syncSignals(delta) {
        if (!delta) return;

        this.selectedGateId = delta.selectedGateId || null;
        this.selectedWireIndex = typeof delta.selectedWireIndex === "number" ? delta.selectedWireIndex : -1;

        if (delta.gates) {
            for (let i = 0; i < delta.gates.length; i++) {
                const gs = delta.gates[i];
                const g = this.gateMap.get(gs.id);
                if (g) {
                    g.inputs = gs.inputs;
                    g.output = gs.output;
                }
            }
        }

        if (delta.wireHighs) {
            const len = Math.min(this.wires.length, delta.wireHighs.length);
            for (let i = 0; i < len; i++) {
                this.wires[i].high = delta.wireHighs[i];
            }
        }

        this.sceneDirty = true;
        this.scheduleRender();
    }

    setViewport(panX, panY, zoom, isPanMode) {
        if (typeof panX === "number") this.panX = panX;
        if (typeof panY === "number") this.panY = panY;
        if (typeof zoom === "number") this.zoom = Math.min(CONSTANTS.MAX_ZOOM, Math.max(CONSTANTS.MIN_ZOOM, zoom));
        if (typeof isPanMode === "boolean") this.isPanMode = isPanMode;

        this.updateCursor();
        this.gridDirty = true;
        this.sceneDirty = true;
        this.scheduleRender();
    }

    updateGateBounds(gate) {
        gate.minX = gate.x - 16;
        gate.minY = gate.y - 16;
        gate.maxX = gate.x + CONSTANTS.GATE_WIDTH + 16;
        gate.maxY = gate.y + CONSTANTS.GATE_HEIGHT + 16;
    }

    updateWireBounds(wire) {
        const pts = wire.points;
        if (!pts || pts.length < 2) {
            wire.minX = 0;
            wire.minY = 0;
            wire.maxX = 0;
            wire.maxY = 0;
            return;
        }
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (let i = 0; i < pts.length; i += 2) {
            const x = pts[i];
            const y = pts[i + 1];
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        wire.minX = minX - 10;
        wire.minY = minY - 10;
        wire.maxX = maxX + 10;
        wire.maxY = maxY + 10;
    }

    screenToCircuit(sx, sy) {
        return {
            x: (sx - this.panX) / this.zoom,
            y: (sy - this.panY) / this.zoom
        };
    }

    snapToGrid(val) {
        return Math.round(val / CONSTANTS.GRID_SIZE) * CONSTANTS.GRID_SIZE;
    }

    getOutputPinPosition(gate) {
        return {
            x: gate.x + CONSTANTS.GATE_WIDTH,
            y: gate.y + CONSTANTS.GATE_HEIGHT / 2
        };
    }

    getInputPinPosition(gate, inputIndex) {
        const count = gate.inputs ? gate.inputs.length : 0;
        if (count <= 1) {
            return {
                x: gate.x,
                y: gate.y + CONSTANTS.GATE_HEIGHT / 2
            };
        }
        const spacing = CONSTANTS.GATE_HEIGHT / (count + 1);
        return {
            x: gate.x,
            y: gate.y + spacing * (inputIndex + 1)
        };
    }

    hasOutputPin(gate) {
        return gate.typeUpper !== "LAMP";
    }

    getLocalCoords(e) {
        const rect = this.sceneCanvas.getBoundingClientRect();
        return {
            offsetX: e.clientX - rect.left,
            offsetY: e.clientY - rect.top
        };
    }

    getEffectivePinRadius(isTouch) {
        const basePx = isTouch ? 16 : CONSTANTS.PIN_PICK_RADIUS_PX;
        return Math.min(18, basePx / this.zoom);
    }

    handlePointerDown(e) {
        if (!this.sceneCanvas) return;
        this.updateCanvasSize();
        this.sceneCanvas.setPointerCapture(e.pointerId);
        const { offsetX, offsetY } = this.getLocalCoords(e);
        const isTouch = e.pointerType === "touch";

        this.activePointers.set(e.pointerId, { x: offsetX, y: offsetY });

        if (this.activePointers.size === 2) {
            const pts = Array.from(this.activePointers.values());
            this.lastPinchDist = Math.hypot(pts[1].x - pts[0].x, pts[1].y - pts[0].y);
            this.isPanning = false;
            this.draggingGate = null;
            this.isDrawingTempWire = false;
            return;
        }

        const { x: cx, y: cy } = this.screenToCircuit(offsetX, offsetY);

        if (this.isPanMode || e.button === 1) {
            this.isPanning = true;
            this.panMoved = false;
            this.panStartMouseX = offsetX - this.panX;
            this.panStartMouseY = offsetY - this.panY;
            this.updateCursor();
            return;
        }

        if (e.button === 2) {
            const inputHit = this.findInputPinAt(cx, cy, isTouch);
            if (inputHit) {
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnRightClickInputPinAsync", inputHit.gate.id, inputHit.index);
                }
                return;
            }

            const gateToDelete = this.findGateAt(cx, cy);
            if (gateToDelete) {
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnRightClickGateAsync", gateToDelete.id);
                }
                return;
            }

            const wireIndex = this.findWireAt(cx, cy, isTouch);
            if (wireIndex >= 0) {
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnRightClickWireAsync", wireIndex);
                }
                return;
            }
            return;
        }

        if (e.button !== 0) return;

        const sourceGate = this.findOutputPinAt(cx, cy, isTouch);
        if (sourceGate) {
            this.wiringSourceGate = sourceGate;
            this.isDrawingTempWire = true;
            this.tempWireEndX = cx;
            this.tempWireEndY = cy;
            this.hoverInputGate = null;
            this.hoverInputIndex = -1;
            this.hoverInputValid = false;

            this.selectedGateId = sourceGate.id;
            this.selectedWireIndex = -1;
            this.updateCursor();
            this.sceneDirty = true;
            this.scheduleRender();

            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync("OnWiringStartedAsync", sourceGate.id);
            }
            return;
        }

        const clickedGate = this.findGateAt(cx, cy);
        if (clickedGate) {
            this.selectedGateId = clickedGate.id;
            this.selectedWireIndex = -1;
            this.draggingGate = clickedGate;
            this.hasMovedGate = false;
            this.dragOffsetX = cx - clickedGate.x;
            this.dragOffsetY = cy - clickedGate.y;

            this.updateCursor();
            this.sceneDirty = true;
            this.scheduleRender();
            return;
        }

        const clickedWireIdx = this.findWireAt(cx, cy, isTouch);
        if (clickedWireIdx >= 0) {
            this.selectedWireIndex = clickedWireIdx;
            this.selectedGateId = null;
            this.sceneDirty = true;
            this.scheduleRender();

            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync("OnWireSelectedAsync", clickedWireIdx);
            }
            return;
        }

        const hadSelection = this.selectedGateId !== null || this.selectedWireIndex !== -1;
        this.selectedGateId = null;
        this.selectedWireIndex = -1;
        this.isPanning = true;
        this.panMoved = false;
        this.panStartMouseX = offsetX - this.panX;
        this.panStartMouseY = offsetY - this.panY;

        this.updateCursor();
        this.sceneDirty = true;
        this.scheduleRender();

        if (hadSelection && this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync("OnSelectionClearedAsync");
        }
    }

    handlePointerMove(e) {
        const { offsetX, offsetY } = this.getLocalCoords(e);
        const isTouch = e.pointerType === "touch";

        if (this.activePointers.has(e.pointerId)) {
            this.activePointers.set(e.pointerId, { x: offsetX, y: offsetY });
        }

        if (this.activePointers.size === 2) {
            const pts = Array.from(this.activePointers.values());
            const dist = Math.hypot(pts[1].x - pts[0].x, pts[1].y - pts[0].y);
            if (this.lastPinchDist > 10 && dist > 10) {
                const centerX = (pts[0].x + pts[1].x) / 2;
                const centerY = (pts[0].y + pts[1].y) / 2;
                const factor = dist / this.lastPinchDist;
                this.zoomAt(centerX, centerY, factor);
            }
            this.lastPinchDist = dist;
            return;
        }

        if (this.isPanning) {
            const newPanX = offsetX - this.panStartMouseX;
            const newPanY = offsetY - this.panStartMouseY;
            if (Math.abs(newPanX - this.panX) > 0.5 || Math.abs(newPanY - this.panY) > 0.5) {
                this.panX = newPanX;
                this.panY = newPanY;
                this.panMoved = true;
                this.gridDirty = true;
                this.sceneDirty = true;
                this.scheduleRender();
            }
            return;
        }

        const { x: cx, y: cy } = this.screenToCircuit(offsetX, offsetY);

        if (this.isDrawingTempWire && this.wiringSourceGate) {
            this.tempWireEndX = cx;
            this.tempWireEndY = cy;
            this.updateInputSnap(cx, cy, isTouch);
            this.sceneDirty = true;
            this.scheduleRender();
            return;
        }

        if (this.draggingGate) {
            const newX = cx - this.dragOffsetX;
            const newY = cy - this.dragOffsetY;

            if (Math.abs(newX - this.draggingGate.x) > 1 || Math.abs(newY - this.draggingGate.y) > 1) {
                this.hasMovedGate = true;
            }

            this.draggingGate.x = newX;
            this.draggingGate.y = newY;
            this.updateGateBounds(this.draggingGate);
            this.updateConnectedWiresPreview(this.draggingGate);

            this.sceneDirty = true;
            this.scheduleRender();
        }
    }

    handlePointerUp(e) {
        if (this.sceneCanvas && this.sceneCanvas.hasPointerCapture(e.pointerId)) {
            this.sceneCanvas.releasePointerCapture(e.pointerId);
        }
        this.activePointers.delete(e.pointerId);

        const { offsetX, offsetY } = this.getLocalCoords(e);
        const isTouch = e.pointerType === "touch";

        if (this.isPanning) {
            this.isPanning = false;
            this.updateCursor();
            if (this.panMoved && this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync("OnViewportChangedAsync", this.panX, this.panY, this.zoom);
            }
            return;
        }

        if (this.isDrawingTempWire && this.wiringSourceGate) {
            const { x: cx, y: cy } = this.screenToCircuit(offsetX, offsetY);
            this.updateInputSnap(cx, cy, isTouch);

            const sourceId = this.wiringSourceGate.id;
            const targetGate = this.hoverInputGate;
            const targetIndex = this.hoverInputIndex;

            this.resetWiringState();
            this.updateCursor();
            this.sceneDirty = true;
            this.scheduleRender();

            if (this.dotNetRef) {
                if (targetGate && targetIndex >= 0) {
                    this.dotNetRef.invokeMethodAsync("OnWireConnectionAttemptAsync", sourceId, targetGate.id, targetIndex);
                } else {
                    this.dotNetRef.invokeMethodAsync("OnWireCancelledAsync");
                }
            }
            return;
        }

        if (this.draggingGate) {
            const gate = this.draggingGate;
            const moved = this.hasMovedGate;
            this.draggingGate = null;
            this.hasMovedGate = false;
            this.updateCursor();

            if (moved) {
                gate.x = this.snapToGrid(gate.x);
                gate.y = this.snapToGrid(gate.y);
                this.updateGateBounds(gate);
                this.updateConnectedWiresPreview(gate);
                this.sceneDirty = true;
                this.scheduleRender();

                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnGateMovedAsync", gate.id, gate.x, gate.y);
                }
            } else {
                this.sceneDirty = true;
                this.scheduleRender();
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnGateClickedAsync", gate.id);
                }
            }
        }
    }

    handlePointerCancel(e) {
        this.activePointers.delete(e.pointerId);
        this.isPanning = false;
        this.resetWiringState();

        if (this.draggingGate) {
            const gate = this.draggingGate;
            const moved = this.hasMovedGate;
            this.draggingGate = null;
            this.hasMovedGate = false;

            if (moved) {
                gate.x = this.snapToGrid(gate.x);
                gate.y = this.snapToGrid(gate.y);
                this.updateGateBounds(gate);
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync("OnGateMovedAsync", gate.id, gate.x, gate.y);
                }
            }
        }
        this.updateCursor();
        this.sceneDirty = true;
        this.scheduleRender();
    }

    handleWheel(e) {
        e.preventDefault();
        const { offsetX, offsetY } = this.getLocalCoords(e);
        const factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
        this.zoomAt(offsetX, offsetY, factor);

        if (this.wheelTimeout) clearTimeout(this.wheelTimeout);
        this.wheelTimeout = setTimeout(() => {
            if (!this.disposed && this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync("OnViewportChangedAsync", this.panX, this.panY, this.zoom);
            }
        }, 80);
    }

    zoomAt(screenX, screenY, factor) {
        const { x: cx, y: cy } = this.screenToCircuit(screenX, screenY);
        this.zoom = Math.min(CONSTANTS.MAX_ZOOM, Math.max(CONSTANTS.MIN_ZOOM, this.zoom * factor));
        this.panX = screenX - cx * this.zoom;
        this.panY = screenY - cy * this.zoom;

        this.gridDirty = true;
        this.sceneDirty = true;
        this.scheduleRender();
    }

    updateConnectedWiresPreview(gate) {
        const gid = gate.id;
        for (let i = 0; i < this.wires.length; i++) {
            const w = this.wires[i];
            if (w.sourceId === gid || w.targetId === gid) {
                const src = this.gateMap.get(w.sourceId);
                const tgt = this.gateMap.get(w.targetId);
                if (src && tgt) {
                    const p1 = this.getOutputPinPosition(src);
                    const p2 = this.getInputPinPosition(tgt, w.targetInput);
                    const midX = Math.round(((p1.x + p2.x) / 2) / 10) * 10;
                    w.points = [p1.x, p1.y, midX, p1.y, midX, p2.y, p2.x, p2.y];
                    this.updateWireBounds(w);
                }
            }
        }
    }

    updateInputSnap(cx, cy, isTouch) {
        if (!this.wiringSourceGate) return;

        const snapRadius = Math.min(24, (isTouch ? 24 : CONSTANTS.SNAP_RADIUS_PX) / this.zoom);
        let bestDist = snapRadius;
        let bestGate = null;
        let bestIndex = -1;

        for (let i = 0; i < this.gates.length; i++) {
            const gate = this.gates[i];
            if (cx < gate.minX - snapRadius || cx > gate.maxX + snapRadius ||
                cy < gate.minY - snapRadius || cy > gate.maxY + snapRadius) {
                continue;
            }
            const count = gate.inputs ? gate.inputs.length : 0;
            for (let idx = 0; idx < count; idx++) {
                const pin = this.getInputPinPosition(gate, idx);
                const d = Math.hypot(cx - pin.x, cy - pin.y);
                if (d <= bestDist) {
                    bestDist = d;
                    bestGate = gate;
                    bestIndex = idx;
                }
            }
        }

        this.hoverInputGate = bestGate;
        this.hoverInputIndex = bestIndex;
        if (!bestGate) {
            this.hoverInputValid = false;
            return;
        }

        const isSelf = bestGate.id === this.wiringSourceGate.id;
        const isOccupied = this.occupiedInputs.has(`${bestGate.id}:${bestIndex}`);
        this.hoverInputValid = !isSelf && !isOccupied;
    }

    resetWiringState() {
        this.isDrawingTempWire = false;
        this.wiringSourceGate = null;
        this.hoverInputGate = null;
        this.hoverInputIndex = -1;
        this.hoverInputValid = false;
    }

    findGateAt(cx, cy) {
        for (let i = this.gates.length - 1; i >= 0; i--) {
            const g = this.gates[i];
            if (cx >= g.x && cx <= g.x + CONSTANTS.GATE_WIDTH &&
                cy >= g.y && cy <= g.y + CONSTANTS.GATE_HEIGHT) {
                return g;
            }
        }
        return null;
    }

    findOutputPinAt(cx, cy, isTouch) {
        const hitRadius = this.getEffectivePinRadius(isTouch);
        for (let i = 0; i < this.gates.length; i++) {
            const g = this.gates[i];
            if (!this.hasOutputPin(g)) continue;
            const pin = this.getOutputPinPosition(g);
            if (Math.hypot(cx - pin.x, cy - pin.y) <= hitRadius) {
                return g;
            }
        }
        return null;
    }

    findInputPinAt(cx, cy, isTouch) {
        const hitRadius = this.getEffectivePinRadius(isTouch);
        for (let i = 0; i < this.gates.length; i++) {
            const g = this.gates[i];
            const count = g.inputs ? g.inputs.length : 0;
            for (let idx = 0; idx < count; idx++) {
                const pin = this.getInputPinPosition(g, idx);
                if (Math.hypot(cx - pin.x, cy - pin.y) <= hitRadius) {
                    return { gate: g, index: idx };
                }
            }
        }
        return null;
    }

    findWireAt(cx, cy, isTouch) {
        const tolerance = Math.min(14, (isTouch ? 14 : 8) / this.zoom);
        for (let i = this.wires.length - 1; i >= 0; i--) {
            const w = this.wires[i];
            if (cx < w.minX - tolerance || cx > w.maxX + tolerance ||
                cy < w.minY - tolerance || cy > w.maxY + tolerance) {
                continue;
            }
            const pts = w.points;
            if (!pts || pts.length < 4) continue;
            for (let j = 0; j <= pts.length - 4; j += 2) {
                const dist = this.distanceToSegment(cx, cy, pts[j], pts[j + 1], pts[j + 2], pts[j + 3]);
                if (dist <= tolerance) {
                    return i;
                }
            }
        }
        return -1;
    }

    distanceToSegment(px, py, x1, y1, x2, y2) {
        const dx = x2 - x1;
        const dy = y2 - y1;
        if (dx === 0 && dy === 0) return Math.hypot(px - x1, py - y1);
        let t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
        t = Math.max(0, Math.min(1, t));
        return Math.hypot(px - (x1 + t * dx), py - (y1 + t * dy));
    }

    updateCursor() {
        if (!this.sceneCanvas) return;
        if (this.isPanning || this.isPanMode) {
            this.sceneCanvas.style.cursor = "grabbing";
        } else if (this.isDrawingTempWire) {
            this.sceneCanvas.style.cursor = "crosshair";
        } else if (this.draggingGate) {
            this.sceneCanvas.style.cursor = "move";
        } else {
            this.sceneCanvas.style.cursor = "default";
        }
    }

    scheduleRender() {
        if (this.disposed || this.rafId !== null) return;
        this.rafId = requestAnimationFrame(() => {
            this.rafId = null;
            this.renderFrame();
        });
    }

    renderFrame() {
        if (this.disposed) return;

        if (this.gridDirty && this.gridCtx) {
            this.renderGridLayer();
            this.gridDirty = false;
        }

        if (this.sceneDirty && this.sceneCtx) {
            this.renderSceneLayer();
            this.sceneDirty = false;
        }
    }

    renderGridLayer() {
        const ctx = this.gridCtx;
        const dpr = this.dpr;
        ctx.save();
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, this.width, this.height);

        ctx.translate(this.panX, this.panY);
        ctx.scale(this.zoom, this.zoom);

        const gridSize = CONSTANTS.GRID_SIZE;
        const startX = Math.floor((-this.panX / this.zoom) / gridSize) * gridSize - gridSize;
        const startY = Math.floor((-this.panY / this.zoom) / gridSize) * gridSize - gridSize;
        const endX = startX + this.width / this.zoom + gridSize * 2;
        const endY = startY + this.height / this.zoom + gridSize * 2;

        ctx.beginPath();
        ctx.strokeStyle = "#e2e8f0";
        ctx.lineWidth = 1 / this.zoom;

        for (let x = startX; x <= endX; x += gridSize) {
            ctx.moveTo(x, startY);
            ctx.lineTo(x, endY);
        }
        for (let y = startY; y <= endY; y += gridSize) {
            ctx.moveTo(startX, y);
            ctx.lineTo(endX, y);
        }
        ctx.stroke();
        ctx.restore();
    }

    renderSceneLayer() {
        const ctx = this.sceneCtx;
        const dpr = this.dpr;

        ctx.save();
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, this.width, this.height);

        ctx.translate(this.panX, this.panY);
        ctx.scale(this.zoom, this.zoom);

        const viewMinX = -this.panX / this.zoom;
        const viewMinY = -this.panY / this.zoom;
        const viewMaxX = (this.width - this.panX) / this.zoom;
        const viewMaxY = (this.height - this.panY) / this.zoom;

        ctx.lineCap = "round";
        ctx.lineJoin = "round";

        for (let i = 0; i < this.wires.length; i++) {
            const w = this.wires[i];
            if (w.maxX < viewMinX || w.minX > viewMaxX || w.maxY < viewMinY || w.minY > viewMaxY) {
                continue;
            }
            this.drawWire(ctx, w, i === this.selectedWireIndex);
        }

        if (this.isDrawingTempWire && this.wiringSourceGate) {
            this.drawTemporaryWire(ctx);
        }

        for (let i = 0; i < this.gates.length; i++) {
            const gate = this.gates[i];
            if (gate.maxX < viewMinX || gate.minX > viewMaxX || gate.maxY < viewMinY || gate.minY > viewMaxY) {
                continue;
            }
            this.drawGate(ctx, gate);
        }

        ctx.restore();
    }

    drawWire(ctx, wire, selected) {
        const pts = wire.points;
        if (!pts || pts.length < 4) return;

        ctx.beginPath();
        ctx.moveTo(pts[0], pts[1]);
        for (let j = 2; j < pts.length; j += 2) {
            ctx.lineTo(pts[j], pts[j + 1]);
        }

        // Clamp clearance halo & wire stroke in circuit coordinates so zooming out stays crisp
        ctx.strokeStyle = "#ffffff";
        ctx.lineWidth = Math.min(8, 6 / this.zoom);
        ctx.stroke();

        ctx.strokeStyle = selected ? "#f59e0b" : wire.high ? "#10b981" : "#64748b";
        const baseWidth = selected ? 3.8 : wire.high ? 2.8 : 2.2;
        ctx.lineWidth = Math.min(5, baseWidth / Math.max(0.65, this.zoom));
        ctx.stroke();
    }

    drawTemporaryWire(ctx) {
        const p1 = this.getOutputPinPosition(this.wiringSourceGate);
        let x2 = this.tempWireEndX;
        let y2 = this.tempWireEndY;
        const snapped = this.hoverInputGate !== null && this.hoverInputIndex >= 0;

        if (snapped) {
            const p2 = this.getInputPinPosition(this.hoverInputGate, this.hoverInputIndex);
            x2 = p2.x;
            y2 = p2.y;
        }

        const midX = (p1.x + x2) / 2;

        ctx.save();
        ctx.beginPath();
        ctx.strokeStyle = !snapped ? "#2563eb" : this.hoverInputValid ? "#16a34a" : "#ef4444";
        ctx.lineWidth = 2.5;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.setLineDash([7, 5]);

        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(midX, p1.y);
        ctx.lineTo(midX, y2);
        ctx.lineTo(x2, y2);
        ctx.stroke();
        ctx.restore();
    }

    drawGate(ctx, gate) {
        const selected = gate.id === this.selectedGateId ||
            (this.wiringSourceGate && gate.id === this.wiringSourceGate.id);

        const stroke = selected ? "#2563eb" : "#1e293b";
        const midY = gate.y + CONSTANTS.GATE_HEIGHT / 2;
        const type = gate.typeUpper;

        ctx.lineCap = "round";
        ctx.lineJoin = "round";

        const inputCount = gate.inputs ? gate.inputs.length : 0;

        for (let i = 0; i < inputCount; i++) {
            const pin = this.getInputPinPosition(gate, i);
            const endX = this.getInputLeadEndX(gate, type, pin.y - midY);

            ctx.beginPath();
            ctx.strokeStyle = gate.inputs[i] ? "#16a34a" : "#64748b";
            ctx.lineWidth = 2;
            ctx.moveTo(pin.x, pin.y);
            ctx.lineTo(endX, pin.y);
            ctx.stroke();
        }

        if (this.hasOutputPin(gate)) {
            const outPin = this.getOutputPinPosition(gate);
            ctx.beginPath();
            ctx.strokeStyle = gate.output ? "#10b981" : "#64748b";
            ctx.lineWidth = 2;
            ctx.moveTo(gate.x + CONSTANTS.BODY_RIGHT, outPin.y);
            ctx.lineTo(outPin.x, outPin.y);
            ctx.stroke();
        }

        if (type === "SWITCH") {
            this.drawSwitchSymbol(ctx, gate, selected);
        } else if (type === "LAMP") {
            this.drawLampSymbol(ctx, gate, stroke);
        } else {
            this.drawLogicSymbol(ctx, gate, type, stroke);
        }

        ctx.fillStyle = selected ? "#2563eb" : "#64748b";
        ctx.font = "bold 11px Arial";
        ctx.textAlign = "center";
        ctx.fillText(gate.label || type, gate.x + CONSTANTS.GATE_WIDTH / 2, gate.y + CONSTANTS.GATE_HEIGHT - 1);

        for (let i = 0; i < inputCount; i++) {
            const pin = this.getInputPinPosition(gate, i);
            const hovered = this.hoverInputGate && this.hoverInputGate.id === gate.id && i === this.hoverInputIndex;
            const candidate = this.isDrawingTempWire &&
                (!this.wiringSourceGate || this.wiringSourceGate.id !== gate.id) &&
                !this.occupiedInputs.has(`${gate.id}:${i}`);

            this.drawPin(ctx, pin.x, pin.y, gate.inputs[i], hovered, this.hoverInputValid, candidate);
        }

        if (this.hasOutputPin(gate)) {
            const outPin = this.getOutputPinPosition(gate);
            this.drawPin(ctx, outPin.x, outPin.y, gate.output, false, true, false);
        }
    }

    drawPin(ctx, x, y, value, hovered, valid, candidate) {
        if (hovered) {
            ctx.beginPath();
            ctx.fillStyle = valid ? "rgba(34,197,94,0.30)" : "rgba(239,68,68,0.30)";
            ctx.strokeStyle = valid ? "#16a34a" : "#dc2626";
            ctx.lineWidth = 2;
            ctx.arc(x, y, 10, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
        } else if (candidate) {
            ctx.beginPath();
            ctx.strokeStyle = "#93c5fd";
            ctx.lineWidth = 1.5;
            ctx.arc(x, y, 7, 0, Math.PI * 2);
            ctx.stroke();
        }

        ctx.beginPath();
        ctx.fillStyle = value ? "#16a34a" : "#64748b";
        ctx.strokeStyle = "#ffffff";
        ctx.lineWidth = 1.5;
        ctx.arc(x, y, CONSTANTS.PIN_DOT_RADIUS, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
    }

    getInputLeadEndX(gate, type, dy) {
        const x0 = gate.x + CONSTANTS.BODY_LEFT;
        if (type === "OR" || type === "NOR") {
            return x0 + this.orIndent(dy);
        }
        if (type === "XOR" || type === "XNOR") {
            return x0 - 6 + this.orIndent(dy);
        }
        return x0;
    }

    orIndent(dy) {
        const half = CONSTANTS.BODY_HEIGHT / 2;
        const r = Math.abs(dy) / half;
        if (r >= 1) return 0;
        return 0.75 * CONSTANTS.OR_CURVE * (1 - r * r);
    }

    drawLogicSymbol(ctx, gate, type, stroke) {
        const mid = gate.y + CONSTANTS.GATE_HEIGHT / 2;
        const top = mid - CONSTANTS.BODY_HEIGHT / 2;
        const bottom = mid + CONSTANTS.BODY_HEIGHT / 2;
        const h = CONSTANTS.BODY_HEIGHT;

        const inverted = type === "NAND" || type === "NOR" || type === "NOT" || type === "XNOR";
        const x0 = gate.x + CONSTANTS.BODY_LEFT;
        const tipX = gate.x + CONSTANTS.BODY_RIGHT - (inverted ? CONSTANTS.BUBBLE_RADIUS * 2 : 0);
        const w = tipX - x0;

        ctx.strokeStyle = stroke;
        ctx.lineWidth = 2;

        switch (type) {
            case "AND":
            case "NAND": {
                const r = h / 2;
                ctx.beginPath();
                ctx.moveTo(x0, top);
                ctx.lineTo(tipX - r, top);
                ctx.arc(tipX - r, mid, r, -Math.PI / 2, Math.PI / 2);
                ctx.lineTo(x0, bottom);
                ctx.closePath();
                ctx.stroke();
                break;
            }
            case "OR":
            case "NOR":
            case "XOR":
            case "XNOR": {
                ctx.beginPath();
                ctx.moveTo(x0, top);
                ctx.bezierCurveTo(x0 + w * 0.5, top, tipX - w * 0.15, mid - h * 0.35, tipX, mid);
                ctx.bezierCurveTo(tipX - w * 0.15, mid + h * 0.35, x0 + w * 0.5, bottom, x0, bottom);
                ctx.bezierCurveTo(x0 + CONSTANTS.OR_CURVE, bottom - h / 3, x0 + CONSTANTS.OR_CURVE, top + h / 3, x0, top);
                ctx.stroke();

                if (type === "XOR" || type === "XNOR") {
                    ctx.beginPath();
                    ctx.moveTo(x0 - 6, bottom);
                    ctx.bezierCurveTo(x0 - 6 + CONSTANTS.OR_CURVE, bottom - h / 3, x0 - 6 + CONSTANTS.OR_CURVE, top + h / 3, x0 - 6, top);
                    ctx.stroke();
                }
                break;
            }
            case "NOT":
            case "BUF":
            case "BUFFER": {
                ctx.beginPath();
                ctx.moveTo(x0, top);
                ctx.lineTo(tipX, mid);
                ctx.lineTo(x0, bottom);
                ctx.closePath();
                ctx.stroke();
                break;
            }
            default: {
                ctx.fillStyle = stroke;
                ctx.font = "bold 12px Arial";
                ctx.textAlign = "center";
                ctx.fillText(type, (x0 + gate.x + CONSTANTS.BODY_RIGHT) / 2, mid + 4);
                break;
            }
        }

        if (inverted) {
            ctx.beginPath();
            ctx.strokeStyle = stroke;
            ctx.lineWidth = 2;
            ctx.arc(tipX + CONSTANTS.BUBBLE_RADIUS, mid, CONSTANTS.BUBBLE_RADIUS, 0, Math.PI * 2);
            ctx.stroke();
        }
    }

    drawSwitchSymbol(ctx, gate, selected) {
        const cx = gate.x + 50;
        const cy = gate.y + CONSTANTS.GATE_HEIGHT / 2;
        const on = gate.output;
        const trackHalf = 11;
        const trackThickness = 22;

        ctx.beginPath();
        ctx.strokeStyle = on ? "#16a34a" : "#94a3b8";
        ctx.lineWidth = trackThickness;
        ctx.moveTo(cx - trackHalf, cy);
        ctx.lineTo(cx + trackHalf, cy);
        ctx.stroke();

        if (selected) {
            ctx.beginPath();
            ctx.strokeStyle = "#2563eb";
            ctx.lineWidth = 2;
            ctx.arc(cx - trackHalf, cy, trackThickness / 2 + 3, Math.PI / 2, Math.PI * 1.5);
            ctx.lineTo(cx + trackHalf, cy - trackThickness / 2 - 3);
            ctx.arc(cx + trackHalf, cy, trackThickness / 2 + 3, -Math.PI / 2, Math.PI / 2);
            ctx.closePath();
            ctx.stroke();
        }

        const knobX = on ? cx + trackHalf : cx - trackHalf;
        ctx.beginPath();
        ctx.fillStyle = "#ffffff";
        ctx.strokeStyle = on ? "#15803d" : "#64748b";
        ctx.lineWidth = 1.5;
        ctx.arc(knobX, cy, 8, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = "#ffffff";
        ctx.font = "bold 11px Arial";
        ctx.textAlign = "center";
        ctx.fillText(on ? "1" : "0", on ? cx - trackHalf : cx + trackHalf, cy + 4);

        ctx.beginPath();
        ctx.strokeStyle = on ? "#10b981" : "#64748b";
        ctx.lineWidth = 2;
        ctx.moveTo(cx + trackHalf + trackThickness / 2, cy);
        ctx.lineTo(gate.x + CONSTANTS.BODY_RIGHT, cy);
        ctx.stroke();
    }

    drawLampSymbol(ctx, gate, stroke) {
        const cx = gate.x + 58;
        const cy = gate.y + CONSTANTS.GATE_HEIGHT / 2;
        const radius = 14;
        const on = gate.output;
        const inputCount = gate.inputs ? gate.inputs.length : 0;

        for (let i = 0; i < inputCount; i++) {
            const pin = this.getInputPinPosition(gate, i);
            const dy = pin.y - cy;
            if (Math.abs(dy) >= radius) continue;
            const edgeX = cx - Math.sqrt(radius * radius - dy * dy);

            ctx.beginPath();
            ctx.strokeStyle = gate.inputs[i] ? "#16a34a" : "#64748b";
            ctx.lineWidth = 2;
            ctx.moveTo(gate.x + CONSTANTS.BODY_LEFT, pin.y);
            ctx.lineTo(edgeX, pin.y);
            ctx.stroke();
        }

        if (on) {
            const glowRadii = [30, 24, 19];
            const glowColors = [
                "rgba(251,191,36,0.12)",
                "rgba(251,191,36,0.20)",
                "rgba(251,191,36,0.32)"
            ];
            for (let i = 0; i < glowRadii.length; i++) {
                ctx.beginPath();
                ctx.fillStyle = glowColors[i];
                ctx.arc(cx, cy, glowRadii[i], 0, Math.PI * 2);
                ctx.fill();
            }
        }

        ctx.beginPath();
        ctx.fillStyle = on ? "#fde047" : "#f1f5f9";
        ctx.strokeStyle = on ? "#d97706" : stroke;
        ctx.lineWidth = 2;
        ctx.arc(cx, cy, radius, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();

        const d = radius * 0.62;
        ctx.beginPath();
        ctx.strokeStyle = on ? "#b45309" : "#94a3b8";
        ctx.lineWidth = 1.8;
        ctx.moveTo(cx - d, cy - d);
        ctx.lineTo(cx + d, cy + d);
        ctx.moveTo(cx + d, cy - d);
        ctx.lineTo(cx - d, cy + d);
        ctx.stroke();
    }

    dispose() {
        this.disposed = true;
        if (this.rafId !== null) {
            cancelAnimationFrame(this.rafId);
            this.rafId = null;
        }
        if (this.wheelTimeout) {
            clearTimeout(this.wheelTimeout);
        }
        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
        if (this.sceneCanvas) {
            this.sceneCanvas.removeEventListener("pointerdown", this.onPointerDown);
            this.sceneCanvas.removeEventListener("pointermove", this.onPointerMove);
            this.sceneCanvas.removeEventListener("pointerup", this.onPointerUp);
            this.sceneCanvas.removeEventListener("pointercancel", this.onPointerCancel);
            this.sceneCanvas.removeEventListener("wheel", this.onWheel);
            this.sceneCanvas.removeEventListener("contextmenu", this.onContextMenu);
        }
    }
}

export function initCanvas(viewportId, dotNetRef) {
    disposeCanvas(viewportId);
    const engine = new CircuitCanvasEngine(viewportId, dotNetRef);
    instances.set(viewportId, engine);
    return [engine.width, engine.height];
}

export function syncScene(viewportId, payload) {
    const engine = instances.get(viewportId);
    if (engine) {
        engine.setScene(payload);
    }
}

export function syncSignals(viewportId, deltaPayload) {
    const engine = instances.get(viewportId);
    if (engine) {
        engine.syncSignals(deltaPayload);
    }
}

export function setViewport(viewportId, panX, panY, zoom, isPanMode) {
    const engine = instances.get(viewportId);
    if (engine) {
        engine.setViewport(panX, panY, zoom, isPanMode);
    }
}

export function measureViewport(viewportId) {
    const engine = instances.get(viewportId);
    if (engine) {
        const valid = engine.updateCanvasSize();
        return valid ? [engine.width, engine.height] : [0, 0];
    }
    const el = document.getElementById(viewportId);
    if (!el) return [0, 0];
    const r = el.getBoundingClientRect();
    return [Math.round(r.width), Math.round(r.height)];
}

export function disposeCanvas(viewportId) {
    const engine = instances.get(viewportId);
    if (engine) {
        engine.dispose();
        instances.delete(viewportId);
    }
}