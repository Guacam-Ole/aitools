// Text selection and editing functions for the After text editor

let currentSelection = null;
let currentRange = null;

// Initialize editor when document loads
document.addEventListener('DOMContentLoaded', function () {
    const editor = document.getElementById('afterTextEditor');
    if (editor) {
        // Enable standard editing keyboard shortcuts
        editor.addEventListener('keydown', function (e) {
            // Allow Ctrl+Z (Undo), Ctrl+Y (Redo), Ctrl+A (Select All), etc.
            // These work automatically in contenteditable

            // Prevent Ctrl+B, Ctrl+I, Ctrl+U (bold, italic, underline) if you don't want formatting
            if ((e.ctrlKey || e.metaKey) && ['b', 'i', 'u'].includes(e.key.toLowerCase())) {
                e.preventDefault();
            }
        });

        // Add visual feedback when editing
        editor.addEventListener('focus', function () {
            this.classList.add('editing');
        });

        editor.addEventListener('blur', function () {
            this.classList.remove('editing');
        });
    }
});

window.getSelectedText = function () {
    const selection = window.getSelection();
    if (selection && selection.rangeCount > 0) {
        const selectedText = selection.toString();

        // Store the selection and range for later use
        currentSelection = selection;
        currentRange = selection.getRangeAt(0).cloneRange();

        return selectedText;
    }
    return "";
};

window.replaceSelectedText = function (newText) {
    if (currentRange) {
        // Get the editor element
        const editor = document.getElementById('afterTextEditor');

        // Delete the current selection
        currentRange.deleteContents();

        // Create a text node with the new text
        const textNode = document.createTextNode(newText);

        // Insert the new text
        currentRange.insertNode(textNode);

        // Move cursor to the end of the inserted text
        currentRange.setStartAfter(textNode);
        currentRange.setEndAfter(textNode);

        // Update the selection
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(currentRange);

        // Trigger input event to notify Blazor of the change
        editor.dispatchEvent(new Event('input', { bubbles: true }));

        // Clear the stored range
        currentRange = null;
        currentSelection = null;
    }
};

window.getEditorContent = function () {
    const editor = document.getElementById('afterTextEditor');
    if (editor) {
        return editor.innerText || editor.textContent || "";
    }
    return "";
};

window.setEditorContent = function (content) {
    const editor = document.getElementById('afterTextEditor');
    if (editor) {
        editor.innerText = content;
    }
};
