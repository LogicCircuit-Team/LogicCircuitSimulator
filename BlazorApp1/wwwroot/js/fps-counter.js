// wwwroot/js/fps-counter.js
window.circuitFps = {
    fps: 0,
    frames: 0,
    lastTime: performance.now(),
    init: function () {
        const div = document.createElement('div');
        div.id = 'fps-meter';
        div.style.cssText = 'position:fixed;bottom:12px;left:12px;background:rgba(15,23,42,0.85);color:#22c55e;padding:4px 10px;border-radius:6px;font-family:monospace;font-size:12px;font-weight:bold;z-index:9999;pointer-events:none;border:1px solid #334155;';
        div.innerText = 'FPS: --';
        document.body.appendChild(div);

        const loop = (time) => {
            this.frames++;
            if (time - this.lastTime >= 1000) {
                this.fps = Math.round((this.frames * 1000) / (time - this.lastTime));
                div.innerText = `FPS: ${this.fps}`;
                div.style.color = this.fps < 30 ? '#ef4444' : this.fps < 50 ? '#f59e0b' : '#22c55e';
                this.frames = 0;
                this.lastTime = time;
            }
            requestAnimationFrame(loop);
        };
        requestAnimationFrame(loop);
    }
};
window.circuitFps.init();