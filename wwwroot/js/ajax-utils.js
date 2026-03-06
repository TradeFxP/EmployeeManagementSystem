/**
 * ajax-utils.js - Centralized AJAX and UI utility functions
 * Provides consistent error handling, CSRF token management, and toast notifications.
 * All AJAX calls across the application should use these helpers.
 */

// ═══════════════════════════════════════════════════════
//  CSRF TOKEN HELPER
// ═══════════════════════════════════════════════════════

/**
 * Get the anti-forgery token from the page.
 * @returns {string} The CSRF token value, or empty string if not found.
 */
function getAntiForgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
}

// ═══════════════════════════════════════════════════════
//  AJAX WRAPPERS
// ═══════════════════════════════════════════════════════

/**
 * Perform a POST request with automatic CSRF token and error handling.
 * @param {string} url - The endpoint URL.
 * @param {object} data - The request payload (will be JSON-serialized).
 * @param {object} [options] - Optional overrides: { contentType, showErrorToast }
 * @returns {Promise<object>} The parsed response data.
 */
function ajaxPost(url, data, options = {}) {
    const { contentType = 'application/json', showErrorToast = true } = options;

    return $.ajax({
        url: url,
        type: 'POST',
        contentType: contentType,
        data: contentType === 'application/json' ? JSON.stringify(data) : data,
        headers: {
            'RequestVerificationToken': getAntiForgeryToken()
        }
    }).fail(function (xhr) {
        if (showErrorToast) {
            const msg = xhr.responseJSON?.message || xhr.responseText || 'An unexpected error occurred.';
            showAppToast(msg, 'error');
        }
    });
}

/**
 * Perform a GET request with error handling.
 * @param {string} url - The endpoint URL.
 * @param {object} [params] - Optional query parameters.
 * @param {object} [options] - Optional overrides: { showErrorToast }
 * @returns {Promise<object>} The parsed response data.
 */
function ajaxGet(url, params = {}, options = {}) {
    const { showErrorToast = true } = options;

    return $.ajax({
        url: url,
        type: 'GET',
        data: params
    }).fail(function (xhr) {
        if (showErrorToast) {
            const msg = xhr.responseJSON?.message || 'Failed to load data.';
            showAppToast(msg, 'error');
        }
    });
}

// ═══════════════════════════════════════════════════════
//  TOAST NOTIFICATION SYSTEM
// ═══════════════════════════════════════════════════════

/**
 * Show a toast notification to the user.
 * @param {string} message - The message to display.
 * @param {'success'|'error'|'warning'|'info'} [type='success'] - Toast type.
 * @param {number} [duration=4000] - Auto-dismiss duration in ms.
 */
function showAppToast(message, type = 'success', duration = 4000) {
    // Remove existing toasts
    document.querySelectorAll('.app-toast').forEach(el => el.remove());

    const colors = {
        success: { bg: '#10b981', icon: '✓' },
        error: { bg: '#ef4444', icon: '✕' },
        warning: { bg: '#f59e0b', icon: '⚠' },
        info: { bg: '#3b82f6', icon: 'ℹ' }
    };

    const { bg, icon } = colors[type] || colors.info;

    const toast = document.createElement('div');
    toast.className = 'app-toast';
    toast.style.cssText = `
        position: fixed; top: 20px; right: 20px; z-index: 99999;
        background: ${bg}; color: #fff; padding: 12px 20px;
        border-radius: 8px; font-size: 14px; font-weight: 500;
        box-shadow: 0 4px 12px rgba(0,0,0,0.25);
        display: flex; align-items: center; gap: 8px;
        animation: slideInRight 0.3s ease-out;
        max-width: 400px; word-wrap: break-word;
    `;
    toast.innerHTML = `<span style="font-size:16px">${icon}</span> ${message}`;

    // Add animation keyframes if not already present
    if (!document.querySelector('#app-toast-styles')) {
        const style = document.createElement('style');
        style.id = 'app-toast-styles';
        style.textContent = `
            @keyframes slideInRight { from { transform: translateX(100%); opacity: 0; } to { transform: translateX(0); opacity: 1; } }
            @keyframes slideOutRight { from { transform: translateX(0); opacity: 1; } to { transform: translateX(100%); opacity: 0; } }
        `;
        document.head.appendChild(style);
    }

    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.animation = 'slideOutRight 0.3s ease-in';
        setTimeout(() => toast.remove(), 300);
    }, duration);
}

// ═══════════════════════════════════════════════════════
//  UTILITY HELPERS
// ═══════════════════════════════════════════════════════

/**
 * Debounce a function call.
 * @param {Function} func - The function to debounce.
 * @param {number} delay - Delay in milliseconds.
 * @returns {Function} Debounced function.
 */
function debounce(func, delay = 300) {
    let timer;
    return function (...args) {
        clearTimeout(timer);
        timer = setTimeout(() => func.apply(this, args), delay);
    };
}

/**
 * Safely parse JSON without throwing.
 * @param {string} str - The JSON string.
 * @param {*} [fallback=null] - Value to return on parse failure.
 * @returns {*} Parsed object or fallback.
 */
function safeJsonParse(str, fallback = null) {
    try {
        return JSON.parse(str);
    } catch {
        return fallback;
    }
}

/**
 * Escapes HTML special characters to prevent XSS.
 * @param {string} str - The string to escape.
 * @returns {string} The escaped string.
 */
function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

// Global exposure for backward compatibility
window.showToast = showAppToast;
window.showOrgToast = showAppToast;
window.escapeHtml = escapeHtml;
window.getAntiForgeryToken = getAntiForgeryToken;
window.ajaxPost = ajaxPost;
window.ajaxGet = ajaxGet;
