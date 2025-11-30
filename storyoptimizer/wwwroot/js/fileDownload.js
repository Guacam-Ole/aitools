// Function to download files from Blazor
window.downloadFile = function (filename, contentType, content) {
    // Create a blob from the content
    const blob = new Blob([content], { type: contentType });

    // Create a temporary URL for the blob
    const url = window.URL.createObjectURL(blob);

    // Create a temporary anchor element and trigger download
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();

    // Clean up
    document.body.removeChild(anchor);
    window.URL.revokeObjectURL(url);
};
