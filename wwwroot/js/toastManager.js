class ToastManager {
    constructor() {
        this.toasts = [];
        this.maxToasts = 5;
        this.defaultDuration = 5000;
        this.init();
    }

    init() {
        let container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.className = 'toast-container';
            document.body.appendChild(container);
        }
    }

    showToast(message, type, title, toastDuration) {
        if (!title) {
            title = {
                'error': 'Error',
                'warning': 'Warning',
                'success': 'Success',
                'info': 'Information'
            }[type] || 'Notification';
        }

        const icons = {
            'error': `<svg fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>`,
            'warning': `<svg fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path>
            </svg>`,
            'success': `<svg fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>`,
            'info': `<svg fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
            </svg>`
        };

        const toastId = 'toast-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
        const toastHtml = `
            <div id="${toastId}" class="toast toast-${type}" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="toast-icon">
                    ${icons[type] || icons['info']}
                </div>
                <div class="toast-content">
                    <div class="toast-title">${this.escapeHtml(title)}</div>
                    <div class="toast-message">${this.escapeHtml(message)}</div>
                </div>
                <button class="toast-close" aria-label="Close">
                    <svg fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                    </svg>
                </button>
            </div>
        `;

        const container = document.getElementById('toastContainer');
        if (container) {
            container.insertAdjacentHTML('beforeend', toastHtml);
        } else {
            this.init();
            document.getElementById('toastContainer').insertAdjacentHTML('beforeend', toastHtml);
        }

        const toastElement = document.getElementById(toastId);
        if (!toastElement) return null;

        if (this.toasts.length >= this.maxToasts) {
            const oldestToast = this.toasts.shift();
            if (oldestToast) {
                this.dismissToast(oldestToast.id);
            }
        }

        const toastInfo = {
            id: toastId,
            element: toastElement,
            timerId: null,
            isPaused: false
        };
        this.toasts.push(toastInfo);

        const closeButton = toastElement.querySelector('.toast-close');
        if (closeButton) {
            closeButton.addEventListener('click', () => {
                this.dismissToast(toastId);
            });
        }

        toastElement.addEventListener('mouseenter', () => {
            this.pauseToast(toastId);
        });

        toastElement.addEventListener('mouseleave', () => {
            this.resumeToast(toastId);
        });

        this.startDismissTimer(toastId, toastDuration);
        return toastId;
    }

    showError(message, title = 'Error', duration = null) {
        return this.showToast(message, 'error', title, duration || 8000);
    }

    showWarning(message, title = 'Warning', duration = null) {
        return this.showToast(message, 'warning', title, duration || 6000);
    }

    showSuccess(message, title = 'Success', duration = null) {
        return this.showToast(message, 'success', title, duration || 5000);
    }

    showInfo(message, title = 'Information', duration = null) {
        return this.showToast(message, 'info', title, duration || 5000);
    }

    startDismissTimer(toastId, duration) {
        const toast = this.toasts.find(t => t.id === toastId);
        if (!toast) return;

        toast.timerId = setTimeout(() => {
            this.dismissToast(toastId);
        }, duration);
    }

    pauseToast(toastId) {
        const toast = this.toasts.find(t => t.id === toastId);
        if (!toast || toast.isPaused) return;

        clearTimeout(toast.timerId);
        toast.isPaused = true;
    }

    resumeToast(toastId) {
        const toast = this.toasts.find(t => t.id === toastId);
        if (!toast || !toast.isPaused) return;

        toast.isPaused = false;
        this.startDismissTimer(toastId, this.defaultDuration / 2);
    }

    dismissToast(toastId) {
        const toastIndex = this.toasts.findIndex(t => t.id === toastId);
        if (toastIndex === -1) return;

        const toast = this.toasts[toastIndex];

        if (toast.timerId) {
            clearTimeout(toast.timerId);
        }

        toast.element.classList.add('toast-hiding');
        setTimeout(() => {
            if (toast.element.parentNode) {
                toast.element.parentNode.removeChild(toast.element);
            }
        }, 300);

        this.toasts.splice(toastIndex, 1);
    }

    dismissAll() {
        const toastIds = this.toasts.map(t => t.id);
        toastIds.forEach(id => this.dismissToast(id));
    }

    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Global instance
window.toastManager = new ToastManager();