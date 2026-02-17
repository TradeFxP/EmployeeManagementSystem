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
// Render history timeline
function renderHistoryTimeline(history) {
    const container = document.getElementById('historyTimeline');

    if (!history || history.length === 0) {
        container.innerHTML = '<p class="text-muted text-center py-4">No history available for this task</p>';
        return;
    }

    // Sort history by date descending (should already be sorted from backend)
    history.sort((a, b) => new Date(b.changedAt) - new Date(a.changedAt));

    let html = '';

    for (let i = 0; i < history.length; i++) {
        const event = history[i];

        // Calculate duration since the *previous* event (which is the next item in the array due to desc sort)
        // For the oldest event (last in array), duration is 0 or undefined unless we want to show time since creation?
        // But for "Moved" events, we might want to see how long it stayed in the PREVIOUS state.

        let durationHtml = '';

        // Strategy: 
        // 1. If the event has 'TimeSpentInSeconds' from backend (e.g. column move), use that.
        // 2. Otherwise, calculate difference between this event and the *next* event in the list (chronologically previous).

        let timeLabel = "Duration";
        let durationText = "";

        if (event.timeSpentInSeconds && event.timeSpentInSeconds > 0) {
            // Backend provided duration (e.g. for column moves)
            timeLabel = "Time in previous status";
            durationText = formatDuration(event.timeSpentInSeconds);
        } else if (i < history.length - 1) {
            // Calculate relative to the previous chronological event (next in this list)
            const currentEventDate = new Date(event.changedAt);
            const prevEventDate = new Date(history[i + 1].changedAt);

            const diffMs = currentEventDate - prevEventDate;
            const diffSeconds = Math.floor(diffMs / 1000);

            if (diffSeconds > 0) {
                timeLabel = "Time since last update";
                durationText = formatDuration(diffSeconds);
            }
        } else {
            // This is the first event (creation) - no previous duration
            durationText = "Task Created";
            timeLabel = "";
        }

        if (durationText && timeLabel) {
            durationHtml = `
                <div class="time-spent badge bg-light text-dark mt-1 border">
                    <i class="bi bi-hourglass-split me-1"></i> ${timeLabel}: <strong>${durationText}</strong>
                </div>
            `;
        } else if (durationText === "Task Created") {
            durationHtml = `
                <div class="badge bg-success-subtle text-success mt-1 border border-success-subtle">
                    <i class="bi bi-stars me-1"></i> Initial Creation
                </div>
            `;
        }


        html += `
        <div class="history-event">
            <div class="event-icon ${getEventClass(event.changeType)}">
                ${getEventIcon(event.changeType)}
            </div>
            <div class="event-content">
                <div class="event-header d-flex justify-content-between align-items-start">
                    <div>
                        <strong>${getEventTitle(event)}</strong>
                        <div class="text-muted" style="font-size: 0.8rem;">
                            <i class="bi bi-clock me-1"></i> ${formatExactDateTime(event.changedAt)}
                        </div>
                    </div>
                    <span class="badge bg-secondary-subtle text-secondary small">${formatRelativeTime(event.changedAt)}</span>
                </div>
                
                <div class="event-details text-muted small mt-2">
                    ${getEventDetails(event)}
                </div>
                
                ${durationHtml}
            </div>
        </div>
        `;
    }

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
        0: '<i class="bi bi-plus-lg"></i>',                // Created
        1: '<i class="bi bi-pencil-fill"></i>',            // Updated
        2: '<i class="bi bi-person-fill-add"></i>',        // Assigned
        3: '<i class="bi bi-arrow-repeat"></i>',           // StatusChanged
        4: '<i class="bi bi-arrow-right-circle-fill"></i>', // ColumnMoved
        5: '<i class="bi bi-input-cursor-text"></i>',      // FieldValueChanged
        6: '<i class="bi bi-flag-fill"></i>',              // PriorityChanged
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
    let details = `<div class="mb-1"><i class="bi bi-person-circle me-1"></i> <strong>${event.changedByUserName}</strong></div>`;

    switch (event.changeType) {
        case 2: // Assigned
            return `${details}<div>Assigned to: <span class="badge bg-primary-subtle text-primary border border-primary-subtle">${event.newValue}</span></div>`;
        case 4: // ColumnMoved
            return `${details}<div>Moved from <strong>${event.fromColumnName}</strong> <i class="bi bi-arrow-right mx-1"></i> <strong>${event.toColumnName}</strong></div>`;
        case 5: // FieldValueChanged
            return `${details}<div>Field: <strong>${event.fieldChanged}</strong></div><div class="text-break mt-1">Old: <span class="text-danger text-decoration-line-through">${event.oldValue || '(empty)'}</span> <i class="bi bi-arrow-right mx-1"></i> New: <span class="text-success">${event.newValue}</span></div>`;
        case 6: // PriorityChanged
            return `${details}<div>Priority changed: <strong>${event.oldValue}</strong> <i class="bi bi-arrow-right mx-1"></i> <strong>${event.newValue}</strong></div>`;
        case 1: // Updated
            if (event.fieldChanged) {
                // If description is long, truncate or handle gracefully
                let oldVal = event.oldValue || '(empty)';
                let newVal = event.newValue || '(empty)';

                if (oldVal.length > 50) oldVal = oldVal.substring(0, 50) + '...';
                if (newVal.length > 50) newVal = newVal.substring(0, 50) + '...';

                return `${details}<div><strong>${event.fieldChanged}</strong> updated</div><div class="text-muted fst-italic small">"${oldVal}" <i class="bi bi-arrow-right"></i> "${newVal}"</div>`;
            }
            return details;
        case 0: // Created
            return `${details}<div>Task created in column <strong>${event.toColumnName || 'Unknown'}</strong></div>`;
        default:
            return details;
    }
}

// Format Exact Date Time (e.g. 13 Feb 2026, 06:22 PM)
function formatExactDateTime(dateString) {
    if (!dateString) return 'Unknown date';
    const date = new Date(dateString);
    return date.toLocaleString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
    }).toUpperCase();
}

// Format Relative Time (e.g. 2 hours ago)
function formatRelativeTime(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} min ago`;
    if (diffHours < 24) return `${diffHours} hr${diffHours > 1 ? 's' : ''} ago`;
    if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;

    return ''; // For older dates, the exact date is enough
}

// Format duration in seconds to human-readable string
function formatDuration(seconds) {
    if (!seconds || seconds <= 0) return "0s";

    if (seconds < 60) return `${seconds}s`;

    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ${seconds % 60}s`;

    const hours = Math.floor(minutes / 60);
    const remainingMins = minutes % 60;
    if (hours < 24) {
        return `${hours}h ${remainingMins}m`;
    }

    const days = Math.floor(hours / 24);
    const remainingHours = hours % 24;
    return `${days}d ${remainingHours}h ${remainingMins}m`;
}

