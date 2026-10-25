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
