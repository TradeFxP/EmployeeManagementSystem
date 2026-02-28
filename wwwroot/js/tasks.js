// tasks.js
// Single responsibility: load boards into right panel

$(document).ready(function () {

    $(".task-link").on("click", function (e) {
        e.preventDefault();

        // Highlight active item
        $(".task-link").removeClass("active");
        $(this).addClass("active");

        var url = $(this).data("url");

        // Show loading
        $("#taskBoardContainer").html(
            "<div class='text-muted'>Loading...</div>"
        );

        // Load partial view
        $("#taskBoardContainer").load(url);
    });

});


$(document).on("submit", "#addTaskForm", function (e) {
    e.preventDefault();

    $.ajax({
        url: "/Tasks/CreateTask",
        type: "POST",
        data: $(this).serialize(),
        success: function () {

            // Properly close Bootstrap 5 modal
            const modalEl = document.getElementById("addTaskModal");
            const modal = bootstrap.Modal.getInstance(modalEl);
            modal.hide();

            // 🔥 IMPORTANT: cleanup backdrop + body state
            document.body.classList.remove("modal-open");

            const backdrops = document.getElementsByClassName("modal-backdrop");
            while (backdrops.length > 0) {
                backdrops[0].parentNode.removeChild(backdrops[0]);
            }

            // Reload current board (quiet)
            if (window.loadTeamBoard && window.currentTeamName) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        }

    });
});

$(document).on("input", "#taskTitle", function () {
    $("#titleCount").text(50 - $(this).val().length);
});

$(document).on("input", "#taskDescription", function () {
    $("#descCount").text(200 - $(this).val().length);
});


$(document).on("click", ".delete-task", function () {

    if (!confirm("Are you sure you want to delete this task?")) return;

    const id = $(this).data("id");

    $.post("/Tasks/DeleteTask", { id: id })
        .done(() => {
            if (window.loadTeamBoard && window.currentTeamName) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        })
        .fail(() => showToast('Failed to delete task. Please try again.', 'danger'));
});



$(document).on("submit", "#addTaskForm", function (e) {
    e.preventDefault();

    $.ajax({
        url: "/Tasks/CreateTask",
        type: "POST",
        data: $(this).serialize(),
        success: function () {
            $("#addTaskModal").modal("hide");

            // reload current board (quiet)
            if (window.loadTeamBoard && window.currentTeamName) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        }
    });
});




// Toggle task description on title click
$(document).on("click", ".task-title", function () {

    const taskId = $(this).data("id");
    const desc = $("#desc-" + taskId);

    desc.toggleClass("d-none");
});


$(document).on("click", ".task-title", function () {
    const id = $(this).data("task-id");
    $("#task-details-" + id).toggleClass("d-none");
});


//assign task

$(document).on("change", ".assign-task", function () {

    const taskId = $(this).data("id");
    const userId = $(this).val();

    $.post("/Tasks/AssignTask", {
        taskId: taskId,
        userId: userId
    });
});



//inlieedit task

$(document).on("click", ".edit-task", function () {

    const id = $(this).data("id");
    const title = prompt("Edit Title:");
    const desc = prompt("Edit Description:");

    if (!title || !desc) return;

    $.post("/Tasks/EditTask", {
        id: id,
        title: title,
        description: desc
    }).done(() => {
        if (window.loadTeamBoard && window.currentTeamName) {
            window.loadTeamBoard(window.currentTeamName, true);
        }
    });
});


// ========== NEW TASK CREATION WITH PRIORITY & CUSTOM FIELDS ==========

// Open create task modal
async function openCreateTaskModal(columnId) {
    document.getElementById("taskColumnId").value = columnId;
    document.getElementById("taskTitle").value = "";
    document.getElementById("taskDescription").value = "";
    document.getElementById("taskPriority").value = "1"; // Default to Medium
    document.getElementById("taskDueDate").value = "";

    const team = document.getElementById('kanbanBoard')?.dataset.teamName;

    // Render custom fields if available and wait for them to load
    if (typeof renderCustomFieldInputs === 'function') {
        try {
            await renderCustomFieldInputs('customFieldsContainer', {}, team);
        } catch (e) {
            console.error('Failed to render custom fields before showing modal', e);
        }
    }

    const modal = new bootstrap.Modal(document.getElementById('createTaskModal'));
    modal.show();
}

// Submit create task form
function submitCreateTask() {
    const title = document.getElementById("taskTitle")?.value.trim();
    const description = document.getElementById("taskDescription")?.value.trim();
    const columnId = document.getElementById("taskColumnId").value;
    const projectId = document.getElementById("taskProjectId")?.value || null;
    const priority = parseInt(document.getElementById("taskPriority").value);
    const dueDate = document.getElementById("taskDueDate")?.value || null;

    if (!title) {
        showToast('Please enter a task title to continue.', 'warning');
        return;
    }

    if (!columnId) {
        showToast('Column not found — please refresh.', 'danger');
        return;
    }

    // Ensure custom fields are rendered and validated before collecting values
    if (typeof validateCustomFields === 'function' && !validateCustomFields()) {
        return;
    }

    const customFieldValues = (typeof collectCustomFieldValues === 'function')
        ? collectCustomFieldValues()
        : {};

    $.ajax({
        url: "/Tasks/CreateTask",
        method: "POST",
        contentType: "application/json",
        data: JSON.stringify({
            columnId: parseInt(columnId),
            title: title,
            description: description,
            projectId: projectId ? parseInt(projectId) : null,
            priority: priority,
            dueDate: dueDate,
            customFieldValues: customFieldValues
        }),
        success: function (response) {
            if (response && response.success) {
                // reload board (quiet)
                if (window.loadTeamBoard && window.currentTeamName) {
                    window.loadTeamBoard(window.currentTeamName, true);
                }
            } else {
                bootstrap.Modal.getInstance(document.getElementById("createTaskModal"))?.hide();
                // still reload (quiet)
                if (window.loadTeamBoard && window.currentTeamName) {
                    window.loadTeamBoard(window.currentTeamName, true);
                }
            }
        },
        error: function (xhr) {
            const text = xhr.responseText || 'An error occurred while creating the task.';
            showToast(text, 'danger');
        }
    });
}

// Get priority badge HTML
function getPriorityBadge(priority) {
    const badges = {
        0: '<span class="badge bg-secondary">Low</span>',
        1: '<span class="badge bg-info">Medium</span>',
        2: '<span class="badge bg-warning text-dark">High</span>',
        3: '<span class="badge bg-danger">Critical</span>'
    };
    return badges[priority] || '';
}

// ================= ASSIGN TASK UI HELPERS =================

function showAssignUI(taskId) {
    // Hide actions toolbar with a slight fade
    const card = $(`.task-card[data-task-id="${taskId}"]`);
    card.find('.task-actions').addClass('d-none');

    // Show assign panel
    const panel = card.find('.task-assign-panel');
    panel.removeClass('d-none');

    // Focus the select for better UX
    panel.find('.task-assign-select').focus();
}

function cancelAssignTask(taskId) {
    const card = $(`.task-card[data-task-id="${taskId}"]`);

    // Hide assign panel
    card.find('.task-assign-panel').addClass('d-none');

    // Restore actions toolbar
    card.find('.task-actions').removeClass('d-none');
}

function confirmAssignTask(taskId) {
    const card = $(`.task-card[data-task-id="${taskId}"]`);
    const select = card.find('.task-assign-select');
    const rawValue = select.val();

    if (!rawValue) {
        showToast('Please select a team member or a team to assign this task.', 'warning');
        return;
    }

    // Determine if it's a user or team assignment
    const type = rawValue.split(':')[0]; // 'user' or 'team'
    const id = rawValue.split(':')[1];

    // Show loading state on button
    const saveBtn = card.find('.task-assign-panel button.btn-primary');
    const originalHtml = saveBtn.html();
    saveBtn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span>');

    let url = "/Tasks/AssignTask";
    let payload = { taskId: taskId, userId: id };

    if (type === 'team') {
        url = "/Tasks/AssignTaskToTeam";
        payload = { taskId: taskId, teamName: id };
    }

    $.ajax({
        url: url,
        type: "POST",
        contentType: (type === 'team') ? "application/json" : "application/x-www-form-urlencoded",
        data: (type === 'team') ? JSON.stringify(payload) : payload,
        success: function (response) {
            if (response.success) {
                showToast('✅ Task successfully assigned!', 'success');

                // Hide panel and restore buttons immediately for responsiveness
                cancelAssignTask(taskId);

                // Board will update automatically via SignalR broadcast
            } else {
                showToast(response.message || 'Failed to assign task.', 'danger');
                saveBtn.prop('disabled', false).html(originalHtml);
            }
        },
        error: function (xhr) {
            const msg = xhr.responseText || 'Error: Could not reach the server.';
            showToast(msg, 'danger');
            saveBtn.prop('disabled', false).html(originalHtml);
        }
    });
}

// ═══════════════════════════════════════════════
// REVIEW WORKFLOW FUNCTIONS
// ═══════════════════════════════════════════════

function openReviewModal(taskId, taskTitle) {
    document.getElementById('reviewTaskId').value = taskId;
    document.getElementById('reviewTaskTitle').textContent = taskTitle;
    document.getElementById('reviewNote').value = '';
    document.querySelector('#reviewPass').checked = true;

    const modal = new bootstrap.Modal(document.getElementById('reviewTaskModal'));
    modal.show();
}

function submitReview() {
    const taskId = parseInt(document.getElementById('reviewTaskId').value);
    const decision = document.querySelector('input[name="reviewDecision"]:checked').value;
    const note = document.getElementById('reviewNote').value.trim();
    const passed = decision === 'pass';

    if (!passed && !note) {
        showToast('Please provide a review note explaining why this task failed.', 'warning');
        return;
    }

    const btn = document.getElementById('submitReviewBtn');
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Submitting...';

    $.ajax({
        url: '/Tasks/ReviewTask',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            TaskId: taskId,
            Passed: passed,
            ReviewNote: note
        }),
        success: function (response) {
            bootstrap.Modal.getInstance(document.getElementById('reviewTaskModal'))?.hide();

            if (response.passed) {
                showToast('✅ Review Passed! Task automatically moved to Completed.', 'success');
            } else {
                showToast('❌ Review Failed. Task returned to previous column.', 'warning');
            }

            // Reload board (If SignalR is not active or connection not established)
            // Note: SignalR handleTaskReviewedUpdate will handle the update for everyone.
            // But we reload for the reviewer anyway to ensure full sync, or we can skip it.
            // For now, let's keep it to be safe, but SignalR will also trigger.
            // Actually, let's skip it IF taskHubConnection exists and is connected.
            if (typeof taskHubConnection !== 'undefined' && taskHubConnection.state === 'Connected') {
                console.log("SignalR will handle the UI update.");
            } else {
                if (window.currentTeamName && window.loadTeamBoard) {
                    window.loadTeamBoard(window.currentTeamName, true);
                }
            }
        },
        error: function (xhr) {
            showToast(xhr.responseText || 'Review failed', 'danger');
        },
        complete: function () {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-send me-1"></i> Submit Review';
        }
    });
}

// ═══════════════════════════════════════════════
// ARCHIVE FUNCTIONS
// ═══════════════════════════════════════════════

function archiveCompletedTasks(teamName) {
    if (!confirm('Archive all completed & passed tasks to the history column?')) return;

    $.ajax({
        url: '/Tasks/ArchiveCompletedTasks',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ TeamName: teamName }),
        success: function (response) {
            showToast(`Archived ${response.archivedCount} task(s) to history`, 'success');
            if (window.currentTeamName && window.loadTeamBoard) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        },
        error: function (xhr) {
            showToast(xhr.responseText || 'Archive failed', 'danger');
        }
    });
}

function archiveSingleTask(taskId) {
    if (!confirm('Archive this task to the history column?')) return;

    $.ajax({
        url: '/Tasks/ArchiveSingleTask',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(taskId),
        success: function () {
            showToast('Task archived to history', 'success');
            if (window.currentTeamName && window.loadTeamBoard) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        },
        error: function (xhr) {
            showToast(xhr.responseText || 'Archive failed', 'danger');
        }
    });
}

// ═══════════════════════════════════════════════
// HISTORY FUNCTIONS
// ═══════════════════════════════════════════════

function loadArchivedTasks(teamName) {
    const container = document.getElementById('historyTasksList');
    if (!container) return; // Guard against missing container

    // Keep spinner if already there or set it
    if (!container.querySelector('.spinner-border')) {
        container.innerHTML = '<div class="text-center py-3"><div class="spinner-border spinner-border-sm text-info"></div></div>';
    }

    $.ajax({
        url: '/Tasks/GetArchivedTasks',
        method: 'GET',
        data: { team: teamName },
        success: function (tasks) {
            if (!tasks || tasks.length === 0) {
                container.innerHTML = '<div class="text-muted text-center small p-4"><i class="bi bi-inbox" style="font-size:24px;"></i><br/>No archived tasks</div>';
                return;
            }

            let html = '';
            tasks.forEach(function (t) {
                const completedDate = t.completedAt ? new Date(t.completedAt).toLocaleDateString('en-GB', {
                    day: '2-digit', month: 'short', year: 'numeric'
                }) : '';

                // Unified Card Design (matches .task-card style but simplified for read-only)
                html += `
                    <div class="task-card history-card" 
                         data-task-id="${t.id}"
                         data-created-at="${t.archivedAt || t.completedAt || ''}"
                         onclick="openArchivedTaskDetail(${t.id})">
                        
                        <div class="card-top-row mb-2">
                            <span class="priority-pill priority-${(t.priority === 3 ? 'critical' : t.priority === 2 ? 'high' : t.priority === 1 ? 'medium' : 'low')}">
                                ${(t.priority === 3 ? 'Critical' : t.priority === 2 ? 'High' : t.priority === 1 ? 'Medium' : 'Low')}
                            </span>
                            <div class="d-flex flex-column align-items-end gap-1">
                                <span class="review-badge review-badge-pass">
                                    <i class="bi bi-check-circle-fill"></i> PASS
                                </span>
                                ${window.isAdmin ? `
                                <button class="btn btn-xs btn-outline-danger border-0 p-0 px-1 d-flex align-items-center gap-1" 
                                        onclick="deleteArchivedTask(event, ${t.id})" 
                                        title="Delete Permanently">
                                    <i class="bi bi-trash-fill" style="font-size: 10px;"></i> Delete
                                </button>` : ''}
                            </div>
                        </div>

                        <div class="task-title-text mb-2">${escapeHtml(t.title)}</div>
                        
                        <!-- Hidden description for filtering -->
                        <div class="task-desc-text d-none">${escapeHtml(t.description || '')}</div>
                        
                        <div class="task-meta mt-2">
                             <div><i class="bi bi-person-check me-1"></i> ${escapeHtml(t.completedBy)}</div>
                             <div><i class="bi bi-calendar-check me-1"></i> ${completedDate}</div>
                        </div>

                        <div class="task-actions mt-2">
                             <button class="action-btn action-btn-info w-100 justify-content-center" title="View Details">
                                <i class="bi bi-info-circle"></i> View Details
                             </button>
                        </div>
                    </div>`;
            });
            container.innerHTML = html;
        },
        error: function () {
            container.innerHTML = '<div class="text-danger text-center small p-3">Failed to load history</div>';
        }
    });
}

function openArchivedTaskDetail(taskId) {
    const body = document.getElementById('archivedTaskBody');
    body.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary"></div></div>';

    const modal = new bootstrap.Modal(document.getElementById('archivedTaskModal'));
    modal.show();

    $.ajax({
        url: '/Tasks/GetArchivedTaskDetail',
        method: 'GET',
        data: { id: taskId },
        success: function (t) {
            const customPriorityObj = (t.customFields || []).find(f => f.fieldName && f.fieldName.trim().toLowerCase() === 'priority');
            const effectivePriority = (customPriorityObj && customPriorityObj.value) ? customPriorityObj.value : t.priority;

            const priorityClassMap = { 'low': 'secondary', 'medium': 'info', 'high': 'warning', 'critical': 'danger' };
            const priorityBadgeClass = priorityClassMap[effectivePriority.toLowerCase()] || 'secondary';
            const priorityBadge = `<span class="badge bg-${priorityBadgeClass}">${escapeHtml(effectivePriority.toUpperCase())}</span>`;

            const dueDateBadge = t.dueDate ?
                `<span class="badge bg-light text-dark border d-flex align-items-center gap-1">
                    <i class="bi bi-calendar-event text-danger"></i>
                    Due Date: ${formatDate(t.dueDate)}
                </span>` : '';

            body.innerHTML = `
                <div class="mb-4">
                    <h4 class="mb-1">${escapeHtml(t.title)}</h4>
                    <div class="d-flex gap-2 mt-2 flex-wrap align-items-center">
                        <span class="badge bg-success"><i class="bi bi-check-circle"></i> ${t.reviewStatus}</span>
                        ${priorityBadge}
                        ${dueDateBadge}
                    </div>
                </div>

                ${t.description ? `<div class="mb-3"><h6 class="text-muted">Description</h6><p>${escapeHtml(t.description)}</p></div>` : ''}

                ${t.reviewNote ? `<div class="mb-3"><h6 class="text-muted">Review Note</h6><div class="alert alert-info py-2">${escapeHtml(t.reviewNote)}</div></div>` : ''}

                <div class="row g-3 mb-3">
                    <div class="col-6">
                        <div class="border rounded p-2">
                            <small class="text-muted d-block">Created By</small>
                            <strong>${escapeHtml(t.createdBy || 'N/A')}</strong>
                        </div>
                    </div>
                    <div class="col-6">
                        <div class="border rounded p-2">
                            <small class="text-muted d-block">Assigned To</small>
                            <strong>${escapeHtml(t.assignedTo || 'N/A')}</strong>
                        </div>
                    </div>
                    <div class="col-6">
                        <div class="border rounded p-2">
                            <small class="text-muted d-block">Reviewed By</small>
                            <strong>${escapeHtml(t.reviewedBy || 'N/A')}</strong>
                        </div>
                    </div>
                    <div class="col-6">
                        <div class="border rounded p-2">
                            <small class="text-muted d-block">Completed By</small>
                            <strong>${escapeHtml(t.completedBy || 'N/A')}</strong>
                        </div>
                    </div>
                </div>

                <div class="border-top pt-3">
                    <h6 class="text-muted mb-2">Timeline</h6>
                    <div class="small">
                        ${t.createdAt ? `<div class="mb-1"><i class="bi bi-plus-circle text-primary me-2"></i>Created: ${formatDate(t.createdAt)}</div>` : ''}
                        ${t.assignedAt ? `<div class="mb-1"><i class="bi bi-person text-info me-2"></i>Assigned: ${formatDate(t.assignedAt)}</div>` : ''}
                        ${t.reviewedAt ? `<div class="mb-1"><i class="bi bi-clipboard-check text-warning me-2"></i>Reviewed: ${formatDate(t.reviewedAt)}</div>` : ''}
                        ${t.completedAt ? `<div class="mb-1"><i class="bi bi-check-circle text-success me-2"></i>Completed: ${formatDate(t.completedAt)}</div>` : ''}
                        ${t.archivedAt ? `<div class="mb-1"><i class="bi bi-archive text-secondary me-2"></i>Archived: ${formatDate(t.archivedAt)}</div>` : ''}
                    </div>
                </div>

                ${t.customFields && t.customFields.length > 0 ? `
                    <div class="border-top pt-3 mt-3">
                        <h6 class="text-muted mb-2">Custom Fields</h6>
                        ${t.customFields.map(f => `
                            <div class="small mb-1">
                                <strong>${escapeHtml(f.fieldName || '')}:</strong> 
                                ${(f.value && f.value.startsWith('/Tasks/GetFieldImage'))
                    ? `<div class="mt-1"><img src="${f.value}" class="rounded border" style="max-height:100px; cursor:pointer;" onclick="window.open('${f.value}', '_blank')" /></div>`
                    : escapeHtml(f.value || '')}
                            </div>`).join('')}
                    </div>
                ` : ''}

                ${window.isAdmin ? `
                <div class="mt-4 pt-3 border-top text-end">
                    <button class="btn btn-danger" onclick="deleteArchivedTask(null, ${t.id})">
                        <i class="bi bi-trash-fill me-1"></i> Delete Task Permanently
                    </button>
                </div>
                ` : ''}
            `;
        },
        error: function () {
            body.innerHTML = '<div class="text-danger text-center">Failed to load task details</div>';
        }
    });
}

// ═══════════════════════════════════════════════
// UTILITY FUNCTIONS
// ═══════════════════════════════════════════════

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.appendChild(document.createTextNode(str));
    return div.innerHTML;
}

function formatDate(dateStr) {
    if (!dateStr) return '';
    try {
        return new Date(dateStr).toLocaleDateString('en-GB', {
            day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    } catch {
        return dateStr;
    }
}

function showToast(message, type) {
    type = type || 'info';
    // Remove existing toasts
    document.querySelectorAll('.premium-toast').forEach(t => t.remove());

    const icons = {
        success: 'bi-check-circle-fill',
        danger: 'bi-exclamation-triangle-fill',
        warning: 'bi-exclamation-circle-fill',
        info: 'bi-info-circle-fill'
    };
    const bgColors = {
        success: 'linear-gradient(135deg, #00C851, #007E33)',
        danger: 'linear-gradient(135deg, #ff4444, #CC0000)',
        warning: 'linear-gradient(135deg, #ffbb33, #FF8800)',
        info: 'linear-gradient(135deg, #33b5e5, #0099CC)'
    };

    const toast = document.createElement('div');
    toast.className = 'premium-toast';
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        z-index: 10000;
        padding: 14px 20px;
        border-radius: 12px;
        color: #fff;
        font-weight: 600;
        font-size: 14px;
        box-shadow: 0 8px 32px rgba(0,0,0,0.25);
        animation: toastSlideIn 0.4s cubic-bezier(0.4, 0, 0.2, 1);
        max-width: 420px;
        display: flex;
        align-items: center;
        gap: 10px;
        backdrop-filter: blur(10px);
        background: ${bgColors[type] || bgColors.info};
    `;

    const icon = document.createElement('i');
    icon.className = `bi ${icons[type] || icons.info}`;
    icon.style.fontSize = '18px';

    const text = document.createElement('span');
    text.style.flex = '1';
    text.textContent = message;

    const closeBtn = document.createElement('button');
    closeBtn.innerHTML = '&times;';
    closeBtn.style.cssText = 'background:none;border:none;color:#fff;font-size:18px;cursor:pointer;padding:0 0 0 8px;opacity:0.8;';
    closeBtn.onclick = () => {
        toast.style.animation = 'toastSlideOut 0.3s ease-in forwards';
        setTimeout(() => toast.remove(), 300);
    };

    // Progress bar
    const progress = document.createElement('div');
    progress.style.cssText = 'position:absolute;bottom:0;left:0;height:3px;background:rgba(255,255,255,0.4);border-radius:0 0 12px 12px;animation:toastProgress 5s linear forwards;';

    toast.appendChild(icon);
    toast.appendChild(text);
    toast.appendChild(closeBtn);
    toast.appendChild(progress);
    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.animation = 'toastSlideOut 0.3s ease-in forwards';
        setTimeout(() => toast.remove(), 300);
    }, 5000);
}

// Add toast animations
const toastStyle = document.createElement('style');
toastStyle.textContent = `
    @keyframes toastSlideIn {
        from { transform: translateX(100%) scale(0.95); opacity: 0; }
        to { transform: translateX(0) scale(1); opacity: 1; }
    }
    @keyframes toastSlideOut {
        from { transform: translateX(0) scale(1); opacity: 1; }
        to { transform: translateX(100%) scale(0.95); opacity: 0; }
    }
    @keyframes toastProgress {
        from { width: 100%; }
        to { width: 0%; }
    }
`;
document.head.appendChild(toastStyle);

function deleteArchivedTask(event, taskId) {
    if (event) event.stopPropagation();

    if (!confirm("Are you sure you want to PERMANENTLY delete this archived task? This cannot be undone.")) return;

    $.ajax({
        url: '/Tasks/DeleteTask',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(taskId),
        success: function () {
            showToast('Task deleted permanently', 'success');

            // Close modal if open
            const modalEl = document.getElementById('archivedTaskModal');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }

            // Refresh history
            if (window.currentTeamName) {
                loadArchivedTasks(window.currentTeamName);
            }
        },
        error: function () {
            showToast('Failed to delete task', 'danger');
        }
    });
}
