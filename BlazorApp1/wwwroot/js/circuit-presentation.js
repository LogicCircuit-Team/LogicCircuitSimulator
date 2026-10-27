(() => {
    const contexts = new Map();
    const dialogPanels = new Map();
    let resizing = null;
    let dialogRequest = 0;
    let backdropStart = null;
    const desktop = () => matchMedia("(min-width: 992px)").matches;

    function setPanel(name, open) {
        const home = document.querySelector(".logic-home");
        const panel = document.getElementById(name === "library" ? "component-library" : "analysis-inspector");
        if (!home || !panel) return;
        if (desktop()) {
            home.dataset[name + "Collapsed"] = String(!open);
            panel.inert = !open;
            panel.setAttribute("aria-hidden", String(!open));
        } else {
            panel.inert = false;
            panel.removeAttribute("aria-hidden");
            const drawer = bootstrap.Offcanvas.getOrCreateInstance(panel);
            if (open) drawer.show(); else drawer.hide();
        }
        document.querySelectorAll(`[data-panel-toggle="${name}"]`).forEach(button => button.setAttribute("aria-expanded", String(open)));
    }

    async function openAnalysisDialog(id, trigger = document.activeElement) {
        const dialog = document.getElementById(id);
        if (!(dialog instanceof HTMLDialogElement)) return null;
        const request = ++dialogRequest;
        const panels = [...document.querySelectorAll(".offcanvas-lg.show,.offcanvas-lg.showing")];
        await Promise.all(panels.map(panel => new Promise(resolve => {
            panel.addEventListener("hidden.bs.offcanvas", resolve, { once: true });
            bootstrap.Offcanvas.getOrCreateInstance(panel).hide();
            setTimeout(resolve, 600);
        })));
        if (request !== dialogRequest) return null;
        document.querySelectorAll("dialog.analysis-dialog[open]").forEach(other => {
            if (other !== dialog) other.close();
        });
        if (!dialog.open) {
            const panel = dialog.closest(".inspector");
            if (panel && !dialogPanels.has(panel)) dialogPanels.set(panel, { inert: panel.inert, ariaHidden: panel.getAttribute("aria-hidden") });
            if (panel) { panel.inert = false; panel.removeAttribute("aria-hidden"); }
            dialog.analysisReturnFocus = trigger;
            dialog.showModal();
            document.body.classList.add("analysis-dialog-open");
            dialog.querySelector(".modal-body").scrollTop = 0;
        }
        return dialog;
    }

    async function showTruthTable(trigger) {
        const dialog = await openAnalysisDialog("truth-table-dialog", trigger);
        dialog?.querySelector("[data-truth-generate]")?.click();
    }

    function resizePanel(name, value) {
        const editor = document.querySelector(".editor");
        if (!editor) return;
        const other = document.querySelector(name === "library" ? ".inspector" : ".library").getBoundingClientRect().width;
        const minimum = name === "library" ? 190 : 260;
        const maximum = Math.max(minimum, Math.min(name === "library" ? 380 : 460, editor.clientWidth - other - 280));
        const width = Math.max(minimum, Math.min(maximum, value));
        editor.style.setProperty(`--${name}-width`, width + "px");
        editor.querySelector(`[data-resize-panel="${name}"]`)?.setAttribute("aria-valuenow", Math.round(width));
    }

    document.addEventListener("click", event => {
        const truth = event.target.closest("[data-truth-table]");
        if (truth) { showTruthTable(truth); return; }
        const launch = event.target.closest("[data-analysis-dialog]");
        if (launch) { openAnalysisDialog(launch.dataset.analysisDialog, launch); return; }
        const close = event.target.closest("[data-dialog-close]");
        if (close) { close.closest("dialog")?.close(); return; }
        const expand = event.target.closest("[data-dialog-expand]");
        if (expand) {
            const expanded = expand.closest("dialog").classList.toggle("is-expanded");
            expand.setAttribute("aria-pressed", String(expanded));
            expand.setAttribute("aria-label", expanded ? "Restore window size" : "Expand window");
            expand.title = expanded ? "Restore window size" : "Expand window";
            return;
        }
        if (event.target === backdropStart) {
            const bounds = backdropStart.getBoundingClientRect();
            if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) backdropStart.close();
        }
        backdropStart = null;
        const button = event.target.closest("[data-panel-toggle]");
        if (!button) return;
        const name = button.dataset.panelToggle;
        const home = document.querySelector(".logic-home");
        const panel = document.getElementById(name === "library" ? "component-library" : "analysis-inspector");
        const open = desktop() ? home.dataset[name + "Collapsed"] === "true" : !panel.classList.contains("show");
        setPanel(name, open);
    });

    document.addEventListener("pointerdown", event => {
        backdropStart = event.target instanceof HTMLDialogElement && event.target.classList.contains("analysis-dialog") ? event.target : null;
        const handle = event.target.closest("[data-resize-panel]");
        if (!handle || !desktop()) return;
        const name = handle.dataset.resizePanel;
        const panel = document.querySelector(name === "library" ? ".library" : ".inspector");
        resizing = { name, x: event.clientX, width: panel.getBoundingClientRect().width, handle, pointer: event.pointerId };
        handle.setPointerCapture(event.pointerId);
        document.body.classList.add("resizing-panel");
        event.preventDefault();
    });
    document.addEventListener("close", event => {
        const dialog = event.target;
        if (!(dialog instanceof HTMLDialogElement) || !dialog.classList.contains("analysis-dialog")) return;
        dialog.classList.remove("is-expanded");
        const expand = dialog.querySelector("[data-dialog-expand]");
        expand?.setAttribute("aria-pressed", "false");
        expand?.setAttribute("aria-label", "Expand window");
        if (expand) expand.title = "Expand window";
        const panel = dialog.closest(".inspector");
        const state = dialogPanels.get(panel);
        if (state && !panel.querySelector("dialog[open]")) {
            panel.inert = state.inert;
            if (state.ariaHidden !== null) panel.setAttribute("aria-hidden", state.ariaHidden);
            dialogPanels.delete(panel);
        }
        const anotherOpen = !!document.querySelector("dialog.analysis-dialog[open]");
        document.body.classList.toggle("analysis-dialog-open", anotherOpen);
        if (anotherOpen) return;
        const trigger = dialog.analysisReturnFocus;
        if (trigger?.isConnected && trigger.getClientRects().length && !trigger.closest("[inert]")) trigger.focus();
        else document.querySelector('[data-panel-toggle="analysis"]')?.focus();
    }, true);
    document.addEventListener("change", event => {
        if (event.target.id === "upload" && /\.(sv|v)$/i.test(event.target.files?.[0]?.name || ""))
            openAnalysisDialog("import-hdl-dialog", document.querySelector('[data-panel-toggle="library"]'));
    });
    document.addEventListener("pointermove", event => {
        if (!resizing) return;
        const delta = (event.clientX - resizing.x) * (resizing.name === "library" ? 1 : -1);
        resizePanel(resizing.name, resizing.width + delta);
    });
    const stopResize = () => {
        if (resizing?.handle.hasPointerCapture(resizing.pointer)) resizing.handle.releasePointerCapture(resizing.pointer);
        resizing = null;
        document.body.classList.remove("resizing-panel");
    };
    document.addEventListener("pointerup", stopResize);
    document.addEventListener("pointercancel", stopResize);
    document.addEventListener("keydown", event => {
        const handle = event.target.closest("[data-resize-panel]");
        if (!handle || !["ArrowLeft", "ArrowRight"].includes(event.key)) return;
        const name = handle.dataset.resizePanel;
        const width = document.querySelector(name === "library" ? ".library" : ".inspector").getBoundingClientRect().width;
        resizePanel(name, width + (event.key === "ArrowRight" ? 20 : -20) * (name === "library" ? 1 : -1));
        event.preventDefault();
    });
    matchMedia("(min-width: 992px)").addEventListener("change", () => {
        document.querySelectorAll(".library,.inspector").forEach(panel => {
            panel.inert = false;
            panel.removeAttribute("aria-hidden");
            bootstrap.Offcanvas.getInstance(panel)?.hide();
        });
        const home = document.querySelector(".logic-home");
        if (home) { delete home.dataset.libraryCollapsed; delete home.dataset.analysisCollapsed; }
    });

    window.circuitPresentation = {
        showTruthTable,
        openAnalysisDialog,
        installWires(id) {
            if (contexts.has(id)) return true;
            const canvas = document.getElementById(id)?.querySelector("canvas");
            const context = canvas?.getContext("2d");
            if (!context) return false;
            const names = ["beginPath", "moveTo", "lineTo", "stroke", "arc", "arcTo", "bezierCurveTo", "quadraticCurveTo", "rect", "closePath"];
            const originals = Object.fromEntries(names.map(name => [name, context[name]]));
            const state = { context, originals, points: [], complex: false, skipped: 0 };
            context.beginPath = function (...args) { state.points = []; state.complex = false; return originals.beginPath.apply(this, args); };
            for (const name of ["moveTo", "lineTo"]) context[name] = function (...args) { state.points.push(args.slice(0, 2)); return originals[name].apply(this, args); };
            for (const name of names.slice(4)) context[name] = function (...args) { state.complex = true; return originals[name].apply(this, args); };
            context.stroke = function (...args) {
                const p = state.points;
                const color = String(this.strokeStyle).toLowerCase();
                const wire = !state.complex && p.length === 4 && p[0][1] === p[1][1] && p[1][0] === p[2][0] && p[2][1] === p[3][1];
                if (wire && ["#64748b", "#10b981", "#f59e0b"].includes(color) && [2.2, 3, 4].some(width => Math.abs(this.lineWidth - width) < .001) && this.getLineDash().length === 0) {
                    state.skipped++;
                    return;
                }
                return originals.stroke.apply(this, args);
            };
            contexts.set(id, state);
            return true;
        },
        removeWires(id) {
            const state = contexts.get(id);
            if (!state) return;
            Object.entries(state.originals).forEach(([name, original]) => state.context[name] = original);
            contexts.delete(id);
        },
        wireStats(id) { return { installed: contexts.has(id), suppressed: contexts.get(id)?.skipped || 0 }; }
    };
})();
