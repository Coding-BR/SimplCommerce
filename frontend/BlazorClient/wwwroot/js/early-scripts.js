(function() {
    document.documentElement.classList.remove("no-js");
    document.documentElement.classList.add("js");
})();

// Helper helper for file downloads
window.downloadFile = (filename, contentType, base64) => {
    const link = document.createElement('a');
    link.download = filename;
    link.href = `data:${contentType};base64,${base64}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.uploadDigitalFileDirectly = async function (inputElement, uploadUrl, token) {
    if (!inputElement || !inputElement.files || inputElement.files.length === 0) {
        return { success: false, error: "Nenhum arquivo selecionado." };
    }
    const file = inputElement.files[0];
    const formData = new FormData();
    formData.append("file", file, file.name);

    try {
        const headers = {};
        if (token) headers["Authorization"] = `Bearer ${token}`;

        const response = await fetch(uploadUrl, {
            method: "POST",
            headers: headers,
            body: formData
        });

        if (!response.ok) {
            const errText = await response.text();
            return { success: false, error: `Erro (${response.status}): ${errText}` };
        }

        const data = await response.json();
        return {
            success: true,
            filePath: data.filePath || data.path || data.url || "",
            fileName: data.fileName || file.name,
            fileSize: data.fileSize || file.size
        };
    } catch (ex) {
        return { success: false, error: `Erro no envio: ${ex.message}` };
    }
};
