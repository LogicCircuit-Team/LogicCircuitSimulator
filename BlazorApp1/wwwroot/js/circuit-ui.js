window.downloadTextFile = (fileName, text) => {
    const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};

window.circuitUi = {
    measure: (id) => {
        const element = document.getElementById(id);
        return element ? [element.clientWidth, element.clientHeight] : [0, 0];
    },
    nextFrame: () => new Promise(resolve => requestAnimationFrame(() => resolve()))
};
window.copyText = async function (text) {
    await navigator.clipboard.writeText(text);
};
window.circuitUi.getPointerPosition = function (id, clientX, clientY) {
    const element = document.getElementById(id);
    const bounds = element.getBoundingClientRect();
    return [clientX - bounds.left, clientY - bounds.top];
};
window.circuitUi.viewportObservers = new Map();
window.circuitUi.observeViewport = function (id, reference) {
    const element = document.getElementById(id);
    if (!element) return;
    let scheduled = false;
    const observer = new ResizeObserver(entries => {
        if (scheduled || entries[0].contentRect.width < 50 || entries[0].contentRect.height < 50) return;
        scheduled = true;
        requestAnimationFrame(() => {
            scheduled = false;
            reference.invokeMethodAsync("ResizeViewportAsync").catch(() => {});
        });
    });
    observer.observe(element);
    window.circuitUi.viewportObservers.set(id, observer);
};
window.circuitUi.unobserveViewport = function (id) {
    window.circuitUi.viewportObservers.get(id)?.disconnect();
    window.circuitUi.viewportObservers.delete(id);
};
