// taskHistory.js - Task history modal and timeline rendering

// Open task history modal
async function openTaskHistoryModal(taskId) {
    const modal = new bootstrap.Modal(document.getElementById('taskHistoryModal'));
    document.getElementById('historyModalTaskId').textContent = `Task #${taskId}`;
    modal.show();

    await loadTaskHistory(taskId);
}

// Load task history from server
async function loadTaskHistory(taskId) {
    const container = document.getElementById('historyTimeline');
    container.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></div>';

    try {
        const response = await fetch(`/Tasks/${taskId}/History`);
        if (!response.ok) throw new Error('Failed to load history');

        const history = await response.json();
        renderHistoryTimeline(history);
    } catch (error) {
        console.error('Error loading task history:', error);
        container.innerHTML = '<div class="alert alert-danger">Failed to load history</div>';
    }
}

// Render history timeline
function renderHistoryTimeline(history) {
    const container = document.getElementById('historyTimeline');

    if (history.length === 0) {
        container.innerHTML = '<p class="text-muted text-center py-4">No history available for this task</p>';
        return;
    }

    const html = history.map(event => `
        <div class="history-event">
            <div class="event-icon ${getEventClass(event.changeType)}">
                ${getEventIcon(event.changeType)}
            </div>
            <div class="event-content">
                <div class="event-header">
                    <strong>${getEventTitle(event)}</strong>
                    <span class="text-muted small">${formatDateTime(event.changedAt)}</span>
                </div>
                <div class="event-details text-muted small">
                    ${getEventDetails(event)}
                </div>
                ${event.timeSpentInSeconds ? `
                    <div class="time-spent badge bg-light text-dark mt-1">
                        <i class="bi bi-hourglass"></i> Time spent: ${formatDuration(event.timeSpentInSeconds)}
                    </div>
                ` : ''}
            </div>
        </div>
    `).join('');

    container.innerHTML = html;
}

// Get CSS class for event type
function getEventClass(changeType) {
    const classes = {
        0: 'event-created',    // Created
        1: 'event-updated',    // Updated
        2: 'event-assigned',   // Assigned
        3: 'event-status',     // StatusChanged
        4: 'event-moved',      // ColumnMoved
        5: 'event-field',      // FieldValueChanged
        6: 'event-priority',   // PriorityChanged
        7: 'event-deleted'     // Deleted
    };
    return classes[changeType] || 'event-default';
}

// Get icon for event type
function getEventIcon(changeType) {
    const icons = {
        0: '<i class="bi bi-plus-circle-fill"></i>',       // Created
        1: '<i class="bi bi-pencil-fill"></i>',            // Updated
        2: '<i class="bi bi-person-fill"></i>',            // Assigned
        3: '<i class="bi bi-arrow-repeat"></i>',           // StatusChanged
        4: '<i class="bi bi-arrow-right-circle-fill"></i>', // ColumnMoved
        5: '<i class="bi bi-input-cursor-text"></i>',      // FieldValueChanged
        6: '<i class="bi bi-exclamation-triangle-fill"></i>', // PriorityChanged
        7: '<i class="bi bi-trash-fill"></i>'              // Deleted
    };
    return icons[changeType] || '<i class="bi bi-circle-fill"></i>';
}

// Get human-readable event title
function getEventTitle(event) {
    const titles = {
        0: 'Task Created',
        1: 'Task Updated',
        2: 'Task Assigned',
        3: 'Status Changed',
        4: 'Moved to Another Column',
        5: 'Field Value Changed',
        6: 'Priority Changed',
        7: 'Task Deleted'
    };
    return titles[event.changeType] || 'Task Modified';
}

// Get detailed event description
function getEventDetails(event) {
    let details = `By: <strong>${event.changedByUserName}</strong>`;

    switch (event.changeType) {
        case 2: // Assigned
            return `${details}<br>Assigned to: <strong>${event.newValue}</strong>`;
        case 4: // ColumnMoved
            return `${details}<br>From: <strong>${event.fromColumnName}</strong> → To: <strong>${event.toColumnName}</strong>`;
        case 5: // FieldValueChanged
            return `${details}<br>Field: <strong>${event.fieldChanged}</strong><br>Changed from: "${event.oldValue}" to "${event.newValue}"`;
        case 6: // PriorityChanged
            return `${details}<br>Priority: <strong>${event.oldValue}</strong> → <strong>${event.newValue}</strong>`;
        case 1: // Updated
            if (event.fieldChanged) {
                return `${details}<br>${event.fieldChanged}: "${event.oldValue}" → "${event.newValue}"`;
            }
            return details;
        default:
            return details;
    }
}

// Format date/time
function formatDateTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    // Relative time for recent events
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;

    // Absolute time for older events
    return date.toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// Format duration in seconds to human-readable
function formatDuration(seconds) {
    if (seconds < 60) return `${seconds} second${seconds !== 1 ? 's' : ''}`;

    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes} minute${minutes !== 1 ? 's' : ''}`;

    const hours = Math.floor(minutes / 60);
    const remainingMins = minutes % 60;
    if (hours < 24) {
        return remainingMins > 0
            ? `${hours} hour${hours !== 1 ? 's' : ''} ${remainingMins} min`
            : `${hours} hour${hours !== 1 ? 's' : ''}`;
    }

    const days = Math.floor(hours / 24);
    const remainingHours = hours % 24;
    return remainingHours > 0
        ? `${days} day${days !== 1 ? 's' : ''} ${remainingHours}h`
        : `${days} day${days !== 1 ? 's' : ''}`;
}
