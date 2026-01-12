
// Utility functions and global helpers
class SiteUtilities {
    static debounce(func, wait, immediate) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                timeout = null;
                if (!immediate) func(...args);
            };
            const callNow = immediate && !timeout;
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
            if (callNow) func(...args);
        };
    }

    static apiCall(method, url, data = null) {
        return new Promise((resolve, reject) => {
            const options = {
                method: method,
                headers: {
                    'Content-Type': 'application/json',
                }
            };

            if (data && (method === 'POST' || method === 'PUT')) {
                options.body = JSON.stringify(data);
            }

            fetch(url, options)
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.json();
                })
                .then(data => resolve(data))
                .catch(error => reject(error));
        });
    }

    static formatObjectNames(objectNamesText) {
        if (!objectNamesText) return '';

        return objectNamesText.split(',')
            .map(name => name.trim())
            .filter(name => name)
            .map(name => {
                if (!name.startsWith('[')) {
                    if (name.includes('.')) {
                        const parts = name.split('.');
                        return `[${parts[0]}].[${parts[1]}]`;
                    } else {
                        return `[${name}]`;
                    }
                }
                return name;
            })
            .join(', ');
    }

    static getInputTypeForDataType(dataType) {
        const type = dataType.toLowerCase();

        if (type.includes('int') || type.includes('decimal') || type.includes('numeric') || type.includes('money')) {
            return 'number';
        } else if (type.includes('date') || type.includes('time')) {
            return 'datetime-local';
        } else if (type.includes('bit')) {
            return 'checkbox';
        } else {
            return 'text';
        }
    }

    static getPlaceholderForDataType(dataType) {
        const type = dataType.toLowerCase();

        if (type.includes('int')) {
            return 'Enter number';
        } else if (type.includes('decimal') || type.includes('numeric')) {
            return 'Enter decimal number';
        } else if (type.includes('date') || type.includes('time')) {
            return 'Select date and time';
        } else if (type.includes('varchar') || type.includes('nvarchar') || type.includes('char')) {
            return 'Enter text';
        } else if (type.includes('bit')) {
            return '';
        } else {
            return 'Enter value';
        }
    }
}

// Initialize when document is ready
$(document).ready(function () {
    // Auto-format SQL object names on blur
    $('#objectNames').on('blur', function () {
        const formatted = SiteUtilities.formatObjectNames($(this).val());
        if (formatted) {
            $(this).val(formatted);
        }
    });

    // Ripple Effect Logic
    $(document).on('click', '.btn', function (e) {
        const btn = $(this);
        const ripple = $('<span class="ripple"></span>');

        const diameter = Math.max(btn.outerWidth(), btn.outerHeight());
        const radius = diameter / 2;

        // Calculate click position relative to the button
        // using offset() from jQuery
        const offset = btn.offset();

        ripple.css({
            width: diameter,
            height: diameter,
            left: e.pageX - offset.left - radius + 'px',
            top: e.pageY - offset.top - radius + 'px'
        });

        // Remove existing ripples to prevent accumulation if clicked rapidly
        // or just append. Appending allows multiple ripples.
        // Let's remove old ones first to be clean.
        btn.find('.ripple').remove();

        btn.append(ripple);

        // Remove ripple after animation
        setTimeout(() => {
            ripple.remove();
        }, 600);
    });

    // TempData messages are now handled in _Layout.cshtml to avoid syntax errors in static JS files
});

// Global delegated event handler for Copy Button
$(document).on('click', '.copy-btn', function () {
    const btn = $(this);
    // Try ID first, then class
    let codeElement = document.getElementById('finalCode');
    if (!codeElement) {
        codeElement = btn.closest('.results-card').find('.code-content')[0];
    }

    // Use textContent to get exact text
    const codeContent = codeElement ? codeElement.textContent : '';

    console.log('Copying content length:', codeContent ? codeContent.length : 0);

    if (!codeContent) {
        if (typeof toastManager !== 'undefined') toastManager.showError('No content to copy', 'Error');
        return;
    }

    // Fallback for clipboard API if needed (though usually not necessary on modern browsers)
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(codeContent).then(() => {
            showCopySuccess(btn);
        }).catch(err => {
            console.error('Clipboard API failed:', err);
            fallbackCopyTextToClipboard(codeContent, btn);
        });
    } else {
        fallbackCopyTextToClipboard(codeContent, btn);
    }
});

function showCopySuccess(btn) {
    const originalHtml = btn.html();
    const originalClass = btn.attr('class');

    btn.html(`
        <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
        </svg>
        Copied!
    `);

    btn.addClass('btn-success');

    if (typeof toastManager !== 'undefined') {
        toastManager.showSuccess('SQL Template copied to clipboard!', 'Copied');
    }

    setTimeout(() => {
        btn.html(originalHtml);
        btn.attr('class', originalClass);
    }, 2000);
}

function fallbackCopyTextToClipboard(text, btn) {
    const textArea = document.createElement("textarea");
    textArea.value = text;

    // Avoid scrolling to bottom
    textArea.style.top = "0";
    textArea.style.left = "0";
    textArea.style.position = "fixed";

    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();

    try {
        const successful = document.execCommand('copy');
        if (successful) {
            showCopySuccess(btn);
        } else {
            if (typeof toastManager !== 'undefined') toastManager.showError('Failed to copy', 'Error');
        }
    } catch (err) {
        console.error('Fallback copy failed', err);
        if (typeof toastManager !== 'undefined') toastManager.showError('Failed to copy', 'Error');
    }

    document.body.removeChild(textArea);
}

// File Upload Interactions
$(document).ready(function () {
    const dropZone = $('#dropZone');
    const fileInput = $('#csvFile');
    const fileNameDisplay = $('#fileNameDisplay');

    if (dropZone.length) {
        // Drag specific events
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            dropZone[0].addEventListener(eventName, preventDefaults, false);
        });

        function preventDefaults(e) {
            e.preventDefault();
            e.stopPropagation();
        }

        const highlight = () => dropZone.addClass('active');
        const unhighlight = () => dropZone.removeClass('active');

        ['dragenter', 'dragover'].forEach(eventName => {
            dropZone.on(eventName, highlight);
        });

        ['dragleave', 'drop'].forEach(eventName => {
            dropZone.on(eventName, unhighlight);
        });

        // Robust click handling: Only trigger if NOT clicked on/inside label
        // because the label automatically triggers the input
        $(document).on('click', '#dropZone', function (e) {
            if ($(e.target).closest('label').length === 0 && e.target !== fileInput[0]) {
                fileInput.trigger('click');
            }
        });

        dropZone[0].addEventListener('drop', handleDrop, false);
        fileInput.on('change', function () {
            handleFiles(this.files);
        });
    }

    function handleDrop(e) {
        const dt = e.dataTransfer;
        const files = dt.files;

        if (fileInput.length && files.length > 0) {
            // Setting files property manually
            fileInput[0].files = files;
            // Trigger change event manually because setting .files doesn't trigger it
            fileInput.trigger('change');
        }
    }

    // Reset UI helper
    function resetUploadUI() {
        $('.default-state').show();
        $('.uploading-state').hide();
        $('.success-state').hide();
        fileInput.val('');
    }

    // Handle Remove Button
    $(document).on('click', '#removeFileBtn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        resetUploadUI();
        // Trigger empty change
        fileInput.trigger('change');
    });

    function handleFiles(files) {
        if (files.length > 0) {
            const file = files[0];
            if (file.type === "text/csv" || file.name.endsWith('.csv')) {

                // Show uploading state
                $('.default-state').hide();
                $('.uploading-state').fadeIn();

                // Simulate progress
                let progress = 0;
                const interval = setInterval(() => {
                    progress += Math.floor(Math.random() * 15) + 5;
                    if (progress > 100) progress = 100;

                    $('.progress-bar-fill').css('width', progress + '%');
                    $('.upload-percent').text(progress + '%');

                    if (progress === 100) {
                        clearInterval(interval);
                        setTimeout(() => {
                            // Show success state
                            $('.uploading-state').hide();
                            $('.success-state').fadeIn();

                            $('#fileNameDisplay').text(file.name);
                            $('#fileSizeDisplay').text((file.size / 1024).toFixed(1) + ' KB');

                            // Color validation success
                            $('#dropZone').css('border-color', 'var(--success-color)');
                        }, 500);
                    }
                }, 100);

            } else {
                if (typeof toastManager !== 'undefined') toastManager.showError('Invalid file type. CSV only.', 'Error');
                fileInput.val('');
            }
        }
    }
});