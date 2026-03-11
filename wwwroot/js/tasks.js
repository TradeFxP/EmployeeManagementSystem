// tasks.js
// Single responsibility: load boards into right panel

// Initialize on page load in case board is already present
$(document).ready(function () {
    if (typeof initDigiLeadsFilters === 'function') {
        initDigiLeadsFilters();
    }
});

$(document).ready(function () {

    let currentBoardXhr = null;

    $(".task-link").on("click", function (e) {
        e.preventDefault();

        // Highlight active item
        $(".task-link").removeClass("active");
        $(this).addClass("active");

        var url = $(this).data("url");
        window.currentTeamName = $(this).data("team"); // Set global team name

        // Abort previous request if still pending
        if (currentBoardXhr) {
            currentBoardXhr.abort();
        }

        // Show loading spinner
        $("#taskBoardContainer").html(`
            <div class="d-flex flex-column align-items-center justify-content-center" style="min-height: 400px;">
                <div class="spinner-border text-primary mb-3" role="status" style="width: 3rem; height: 3rem;"></div>
                <div class="text-muted fw-bold">Loading board...</div>
            </div>
        `);

        // Load partial view with explicit AJAX for abort control
        currentBoardXhr = $.ajax({
            url: url,
            type: 'GET',
            success: function (html) {
                $("#taskBoardContainer").html(html);
                currentBoardXhr = null;

                // Initialize plugins for the new board
                if (typeof initDigiLeadsFilters === 'function') {
                    initDigiLeadsFilters();
                }
            },
            error: function (xhr, status, error) {
                if (status === 'abort') return;
                $("#taskBoardContainer").html(
                    "<div class='alert alert-danger m-3'>Failed to load board. Please try again.</div>"
                );
                currentBoardXhr = null;
            }
        });
    });

});

// ─────────────────────────────────────────────────────────────────────────────
// Digi Leads - Calendar Filter Functions
// ─────────────────────────────────────────────────────────────────────────────
function initDigiLeadsFilters() {
    const dateFilterGroup = document.getElementById('leadDateFilterGroup');
    if (dateFilterGroup) {
        flatpickr(dateFilterGroup, {
            wrap: true, // This allows the calendar icon (data-toggle) and input (data-input) to trigger the picker
            dateFormat: "d-m-Y",
            allowInput: true,
            disableMobile: "true", // Better UI on desktop
            onChange: function (selectedDates, dateStr, instance) {
                if (typeof filterBoardTasks === 'function') {
                    filterBoardTasks();
                }
            }
        });
    }
}

function setLeadDateToday() {
    const today = new Date();
    const day = String(today.getDate()).padStart(2, '0');
    const month = String(today.getMonth() + 1).padStart(2, '0');
    const year = today.getFullYear();
    const dateStr = `${day}-${month}-${year}`;

    const input = document.getElementById('leadDateFilter');
    if (input && input._flatpickr) {
        input._flatpickr.setDate(dateStr, true);
    }
}

function clearLeadDateFilter() {
    const group = document.getElementById('leadDateFilterGroup');
    const input = document.getElementById('leadDateFilter');
    const fp = (group && group._flatpickr) || (input && input._flatpickr);

    if (fp) {
        fp.clear();
    } else if (input) {
        input.value = "";
    }

    if (typeof filterBoardTasks === 'function') {
        filterBoardTasks();
    }
}


// Task creation is handled by submitCreateTask function in this file.

$(document).on("input", "#taskTitle", function () {
    $("#titleCount").text(50 - $(this).val().length);
});

$(document).on("input", "#taskDescription", function () {
    $("#descCount").text(200 - $(this).val().length);
});


// Use a centralized deleteTask function
function deleteTask(taskId) {
    if (!confirm('Are you sure you want to delete this task?')) return;

    fetch('/Tasks/DeleteTask', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': window.getAntiForgeryToken()
        },
        body: JSON.stringify(taskId)
    })
        .then(res => {
            if (!res.ok) throw new Error('Failed to delete task');

            // Remove card from DOM immediately
            const card = document.querySelector(`.task-card[data-task-id="${taskId}"]`);
            if (card) {
                card.remove();
                if (typeof updateColumnCounts === 'function') updateColumnCounts();
            }
            if (typeof showToast === 'function') showToast("🗑️ Task deleted successfully!", "info");
        })
        .catch(err => {
            console.error(err);
            if (typeof showToast === 'function') showToast('Failed to delete task.', 'danger');
        });
}

$(document).on("click", ".delete-task", function (e) {
    e.preventDefault();
    e.stopPropagation();
    const id = $(this).data("id");

    $.post("/Tasks/DeleteTask", { id: id })
        .done(() => {
            if (window.loadTeamBoard && window.currentTeamName) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        })
        .fail(() => showToast('Failed to delete task. Please try again.', 'danger'));
});



// Removed duplicate handler




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
    const columnIdInput = document.getElementById("taskColumnId");
    if (columnIdInput) columnIdInput.value = columnId;

    // Reset basic fields
    ["taskTitle", "taskDescription", "taskDueDate"].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = "";
    });

    const priorityEl = document.getElementById("taskPriority");
    if (priorityEl) priorityEl.value = "1"; // Default to Medium

    const board = document.getElementById('kanbanBoard');
    const team = board?.dataset.teamName;
    const showOther = document.getElementById('showOtherCheckbox')?.checked !== false;

    // 🔥 Dynamic Visibility for System Fields
    const priorityVisible = showOther && board?.dataset.priorityVisible !== 'false';
    const dueDateVisible = showOther && board?.dataset.dueDateVisible !== 'false';
    const titleVisible = showOther && board?.dataset.titleVisible !== 'false';
    const descriptionVisible = showOther && board?.dataset.descriptionVisible !== 'false';

    ["Priority", "DueDate", "Title", "Description"].forEach(field => {
        const group = document.getElementById(`groupCreateTask${field}`);
        const visible = eval(`${field.charAt(0).toLowerCase() + field.slice(1)}Visible`);
        if (group) group.style.display = visible ? 'block' : 'none';
    });

    // Render custom fields if available
    if (typeof renderCustomFieldInputs === 'function') {
        try {
            await renderCustomFieldInputs('customFieldsContainer', {}, team);
        } catch (e) {
            console.error('Failed to render custom fields', e);
        }
    }

    const modalEl = document.getElementById('createTaskModal');
    if (modalEl) {
        const modal = new bootstrap.Modal(modalEl);
        modal.show();
    }
}

// Submit create task form
async function submitCreateTask() {
    const titleEl = document.getElementById("taskTitle");
    const title = titleEl ? titleEl.value.trim() : "";
    const columnId = document.getElementById("taskColumnId")?.value;
    const priority = document.getElementById("taskPriority")?.value || "1";
    const dueDate = document.getElementById("taskDueDate")?.value || null;
    const description = document.getElementById("taskDescription")?.value || "";

    const titleGroup = document.getElementById('groupCreateTaskTitle');
    const isTitleVisible = titleGroup && titleGroup.style.display !== 'none';

    if (isTitleVisible && !title) {
        if (typeof showToast === 'function') showToast('Please enter a task title.', 'warning');
        else alert('Please enter a task title.');
        return;
    }

    if (!columnId) {
        if (typeof showToast === 'function') showToast('Column identification failed.', 'danger');
        return;
    }

    // Validate custom fields
    if (typeof validateCustomFields === 'function' && !validateCustomFields('customFieldsContainer')) {
        return;
    }

    const customFieldValues = (typeof collectCustomFieldValues === 'function')
        ? collectCustomFieldValues('customFieldsContainer')
        : {};

    try {
        const response = await fetch("/Tasks/CreateTask", {
            method: "POST",
            headers: {
                'Content-Type': 'application/json',
                "RequestVerificationToken": window.getAntiForgeryToken()
            },
            body: JSON.stringify({
                columnId: parseInt(columnId),
                title: title,
                description: description,
                priority: parseInt(priority),
                dueDate: dueDate,
                assignedToUserId: document.getElementById("taskAssignedToUserId")?.value || null,
                customFieldValues: customFieldValues
            })
        });

        if (response.ok) {
            const modalEl = document.getElementById("createTaskModal");
            const modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();

            if (typeof showToast === 'function') {
                const assignedName = document.getElementById("taskAssignedToUserId")?.getAttribute("data-assigned-name");
                const msg = assignedName
                    ? `✅ Task created and assigned to ${assignedName}!`
                    : "✅ Task created successfully!";
                showToast(msg, "success");
            }

            // Reload board
            const assignedTeam = document.getElementById("taskAssignedToTeam")?.value;
            if (assignedTeam && window.loadTeamBoard && assignedTeam !== window.currentTeamName) {
                if (typeof showToast === 'function') showToast(`Switching to ${assignedTeam} board...`, 'info');
                window.loadTeamBoard(assignedTeam);
            } else if (window.loadTeamBoard && window.currentTeamName) {
                window.loadTeamBoard(window.currentTeamName, true);
            }
        } else {
            const errorText = await response.text();
            throw new Error(errorText || 'Failed to create task');
        }
    } catch (err) {
        console.error(err);
        if (typeof showToast === 'function') showToast(err.message, 'danger');
    }
}

// Open edit task modal
async function openEditTaskModal(taskId) {
    const team = window.currentTeamName;
    try {
        const response = await fetch(`/Tasks/GetTask?id=${taskId}`);
        if (!response.ok) throw new Error("Failed to load task details");
        const task = await response.json();

        // Populate Modal Fields
        const idEl = document.getElementById('editTaskId');
        const titleEl = document.getElementById('editTaskTitle');
        const descEl = document.getElementById('editTaskDescription');
        const priorityEl = document.getElementById('editTaskPriority');
        const assignedEl = document.getElementById('editTaskAssignedTo');
        const dueEl = document.getElementById('editTaskDueDate');

        if (idEl) idEl.value = task.id;
        if (titleEl) titleEl.value = task.title;
        if (descEl) descEl.value = task.description || "";
        if (priorityEl) priorityEl.value = task.priority;
        if (assignedEl) assignedEl.value = task.assignedToUserId || "";
        if (dueEl) dueEl.value = task.dueDate || "";

        const board = document.getElementById('kanbanBoard');
        const showOther = document.getElementById('showOtherCheckbox')?.checked !== false;

        // 🔥 Dynamic Visibility
        const priorityVisible = showOther && board?.dataset.priorityVisible !== 'false';
        const dueDateVisible = showOther && board?.dataset.dueDateVisible !== 'false';
        const titleVisible = showOther && board?.dataset.titleVisible !== 'false';
        const descriptionVisible = showOther && board?.dataset.descriptionVisible !== 'false';

        ["Priority", "DueDate", "Title", "Description"].forEach(field => {
            const group = document.getElementById(`groupEditTask${field}`);
            const visible = eval(`${field.charAt(0).toLowerCase() + field.slice(1)}Visible`);
            if (group) group.style.display = visible ? 'block' : 'none';
        });

        // Restricted Lead Fields for Digi Leads and Sales1
        const isAdmin = window.isAdmin === true || window.currentUserRole === 'Admin';
        const isLeadTeam = team === 'Digi Leads' || team === 'sales1';
        if (isLeadTeam && !isAdmin) {
            if (descEl) descEl.readOnly = true;
            if (titleEl) titleEl.readOnly = true;
        } else {
            if (descEl) descEl.readOnly = false;
            if (titleEl) titleEl.readOnly = false;
        }

        // Render custom fields
        if (typeof renderCustomFieldInputs === 'function') {
            await renderCustomFieldInputs('editCustomFieldsContainer', task.customFieldValues || {}, team, task.description);
        }

        const modalEl = document.getElementById('editTaskModal');
        if (modalEl) {
            const modal = new bootstrap.Modal(modalEl);
            modal.show();

            // Load Communication Buttons for sales1 team
            loadSalesCommunicationButtons(team, task);
        }

    } catch (err) {
        console.error(err);
        if (typeof showToast === 'function') showToast(err.message, 'danger');
    }
}

async function loadSalesCommunicationButtons(team, task) {
    const container = document.getElementById('communicationButtonsContainer');
    if (!container) return;
    container.innerHTML = ''; // Clear previous

    if (team !== 'sales1' && team !== 'Sales1') return;

    try {
        // Load partials
        const [wsRes, emRes] = await Promise.all([
            fetch('/Communication/GetWhatsAppButton'),
            fetch('/Communication/GetEmailButton')
        ]);

        if (wsRes.ok) container.innerHTML += await wsRes.text();
        if (emRes.ok) container.innerHTML += await emRes.text();

        // Setup handlers
        const wsBtn = document.getElementById('whatsappBtn');

        if (wsBtn) {
            wsBtn.onclick = async (e) => {
                e.preventDefault();
                try {
                    await fetch(`/Communication/LogWhatsApp?taskId=${task.id}`, { method: 'POST' });
                } catch (err) {
                    console.error("Failed to log WhatsApp click", err);
                }
                window.open(`/Communication/WhatsApp/${task.id}`, '_blank');
            };
        }

        const zohoDirectBtn = document.getElementById('zohoDirectBtn');

        if (zohoDirectBtn) {
            zohoDirectBtn.onclick = (e) => {
                e.preventDefault();
                if (typeof openEmailTemplateModal === "function") {
                    openEmailTemplateModal(task.id);
                }
            };
        }

        // Dropdown options
        if (true) {
            const gmailOpt = document.getElementById('gmailOption');
            const zohoOpt = document.getElementById('zohoOption');
            const defaultOpt = document.getElementById('defaultMailOption');

            if (gmailOpt) {
                gmailOpt.onclick = (e) => {
                    window.open(`/Communication/Email?id=${task.id}&type=gmail`, '_blank');
                };
            }

            if (zohoOpt) {
                zohoOpt.onclick = (e) => {
                    e.preventDefault();
                    if (typeof openEmailTemplateModal === 'function') {
                        openEmailTemplateModal(task.id);
                    } else {
                        window.open(`/Communication/Email?id=${task.id}&type=zoho`, '_blank');
                    }
                };
            }

            if (defaultOpt) {
                defaultOpt.onclick = (e) => {
                    window.location.href = `/Communication/Email?id=${task.id}&type=default`;
                };
            }
        }

    } catch (err) {
        console.error("Failed to load communication buttons:", err);
    }
}

function submitEditTask() {
    const taskIdEl = document.getElementById('editTaskId');
    if (!taskIdEl) return;
    const taskId = parseInt(taskIdEl.value);
    const titleEl = document.getElementById('editTaskTitle');
    const title = titleEl ? titleEl.value.trim() || "Task" : "Task";

    const descEl = document.getElementById('editTaskDescription');
    const description = descEl ? descEl.value.trim() : "";

    const priority = parseInt(document.getElementById('editTaskPriority').value);
    const assignedToUserId = document.getElementById('editTaskAssignedTo').value;

    const titleGroup = document.getElementById('groupEditTaskTitle');
    const isTitleVisible = titleGroup && titleGroup.style.display !== 'none';

    if (isTitleVisible && (!title || title === "Task")) {
        showToast("Title is required", "warning");
        return;
    }

    // Validate custom fields
    if (typeof validateCustomFields === 'function' && !validateCustomFields('editCustomFieldsContainer')) {
        return;
    }

    const editDueDateVal = document.getElementById('editTaskDueDate') ? document.getElementById('editTaskDueDate').value : null;

    const data = {
        taskId: taskId,
        title: title,
        description: description,
        priority: priority,
        dueDate: editDueDateVal || null,
        assignedToUserId: assignedToUserId,
        customFieldValues: (typeof collectCustomFieldValues === 'function') ?
            collectCustomFieldValues('editCustomFieldsContainer') : {}
    };

    fetch('/Tasks/UpdateTask', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    })
        .then(res => {
            if (!res.ok) throw new Error("Failed to update task");
            return res.json();
        })
        .then(() => {
            showToast("✅ Task updated successfully!", "success");
            // Close modal
            const modalEl = document.getElementById('editTaskModal');
            const modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();

            // Reload board if necessary, though SignalR should handle it
            if (window.currentTeamName && window.loadTeamBoard) loadTeamBoard(window.currentTeamName);
        })
        .catch(err => {
            console.error(err);
            showToast("Failed to update task", "danger");
        });
}

// Redundant function removed as it exists in customFields.js

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
        url: '/TaskReview/ReviewTask',
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
        url: '/TaskReview/ArchiveCompletedTasks',
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
        url: '/TaskReview/ArchiveSingleTask',
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
        url: '/TaskReview/GetArchivedTasks',
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
        url: '/TaskReview/GetArchivedTaskDetail',
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

// Unified escapeHtml is now in ajax-utils.js


function formatDate(dateStr) {
    if (!dateStr) return '';
    try {
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;

        const day = d.getDate().toString().padStart(2, '0');
        const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        const month = months[d.getMonth()];
        const year = d.getFullYear();

        let hours = d.getHours();
        const minutes = d.getMinutes().toString().padStart(2, '0');
        const ampm = hours >= 12 ? 'PM' : 'AM';
        hours = hours % 12 || 12;
        const hStr = hours.toString().padStart(2, '0');

        return `${day} ${month} ${year}, ${hStr}:${minutes} ${ampm}`;
    } catch {
        return dateStr;
    }
}

// Unified showToast is now in ajax-utils.js


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

// ═══════════════════════════════════════════════
// DYNAMIC MOVE-TO LOGIC
// ═══════════════════════════════════════════════

function populateMoveToDropdown() {
    const dropdown = document.getElementById('editTaskMoveTo');
    if (!dropdown) return;

    dropdown.innerHTML = '<option value="">-- Change Column --</option>';

    // Select current column ID if we are editing
    const currentColId = document.getElementById('taskColumnId')?.value ||
        document.querySelector(`.task-card[data-task-id="${document.getElementById('editTaskId')?.value}"]`)?.dataset.columnId;

    document.querySelectorAll('.kanban-column').forEach(col => {
        const id = col.dataset.columnId;
        const name = col.dataset.columnName;
        if (id && name && name.toLowerCase() !== 'history') {
            const isSelected = id == currentColId ? 'selected' : '';
            dropdown.innerHTML += `<option value="${id}" ${isSelected}>${name}</option>`;
        }
    });
}

function moveTaskFromEditModal() {
    const taskId = parseInt(document.getElementById('editTaskId').value);
    const targetColumnId = parseInt(document.getElementById('editTaskMoveTo').value);

    if (!taskId || !targetColumnId) return;

    // Check if it's the same column
    const card = document.querySelector(`.task-card[data-task-id="${taskId}"]`);
    if (card && card.dataset.columnId == targetColumnId) return;

    fetch('/Tasks/MoveTask', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': window.getAntiForgeryToken()
        },
        body: JSON.stringify({
            taskId: taskId,
            columnId: targetColumnId
        })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                // The SignalR "TaskMoved" handler will take care of moving the card in the DOM.
                // But we should update the local data-column-id just in case
                if (card) card.dataset.columnId = targetColumnId;
                if (typeof showToast === 'function') showToast("🚀 Task moved successfully!", "success");

                // Optionally close modal or refresh? User said "when select in move to drop the task will move to that column"
                // We'll keep modal open so they can keep editing, but signal move.
            } else {
                alert("Error moving task: " + (data.message || "Unknown error"));
                // Reset dropdown to previous value
                populateMoveToDropdown();
            }
        })
        .catch(err => {
            console.error("Move error:", err);
            alert("Failed to move task.");
            populateMoveToDropdown();
        });
}
// ═══════════════════════════════════════════════
// VIRTUAL LEAD LOGIC (NO-STORAGE)
// ═══════════════════════════════════════════════

/**
 * Persistent Facebook Leads Integration
 * Now that leads are stored as actual TaskItems, we just refresh the board
 * when new ones are detected via SignalR.
 */
function fetchVirtualLeads() {
    // Legacy virtual leads function - now handled by normal board load
    console.log("Persistent leads are loaded automatically with the board.");
}

function handleNewLeadsUpdate(data) {
    if (window.currentTeamName === 'Digi Leads') {
        console.log("Refreshing Digi Leads board due to new leads...");
        if (typeof loadTeamBoard === 'function') {
            loadTeamBoard('Digi Leads', true); // quiet refresh
        }

        if (typeof showToast === 'function' && data.leads) {
            showToast(`📣 ${data.leads.length} new lead(s) detected!`, 'success');
        }
    }
}

/**
 * Renders a virtual lead card that matches _TaskCard.cshtml appearance
 */

// ═══════════════════════════════════════════════
// OUTLOOK EMAIL TEMPLATE LOGIC
// ═══════════════════════════════════════════════

function openEmailTemplateModal(taskId) {
    document.getElementById('emailTemplateTaskId').value = taskId;
    const templateSelect = document.getElementById('templateSelect');
    const previewContainer = document.getElementById('emailPreviewContainer');
    const previewEmpty = document.getElementById('emailPreviewEmpty');
    const btnNext = document.getElementById('btnNextStep');

    // Reset steps
    const step1 = document.getElementById('emailStep1');
    const step2 = document.getElementById('emailStep2');
    const footer1 = document.getElementById('footerStep1');
    const footer2 = document.getElementById('footerStep2');

    if (step1) step1.classList.remove('d-none');
    if (step2) step2.classList.add('d-none');
    if (footer1) footer1.classList.remove('d-none');
    if (footer2) footer2.classList.add('d-none');

    // Reset UI
    if (templateSelect) templateSelect.innerHTML = '<option value="" disabled selected>-- Choose a Template --</option>';
    if (previewContainer) previewContainer.classList.add('d-none');
    if (previewEmpty) {
        previewEmpty.classList.remove('d-none');
        previewEmpty.innerHTML = '<i class="bi bi-envelope-paper display-4 text-light"></i><p class="mt-2">Select a template to view the content.</p>';
    }
    if (btnNext) btnNext.disabled = true;

    // Fetch templates
    fetch('/Communication/GetEmailTemplates')
        .then(res => res.json())
        .then(templates => {
            if (templates && templates.length > 0) {
                templates.forEach(t => {
                    const option = document.createElement('option');
                    option.value = t;
                    option.textContent = t.replace(/_/g, ' ');
                    templateSelect.appendChild(option);
                });
            } else {
                templateSelect.innerHTML = '<option value="" disabled>No templates found</option>';
            }
        })
        .catch(err => console.error("Error fetching templates:", err));

    const modalEl = document.getElementById('emailTemplateModal');
    if (modalEl) {
        const modal = new bootstrap.Modal(modalEl);
        modal.show();
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const templateSelect = document.getElementById('templateSelect');
    const previewFrame = document.getElementById('emailPreviewFrame');
    const previewContainer = document.getElementById('emailPreviewContainer');
    const previewLoading = document.getElementById('emailPreviewLoading');
    const previewEmpty = document.getElementById('emailPreviewEmpty');

    const btnNext = document.getElementById('btnNextStep');
    const btnBack = document.getElementById('btnBackStep');
    const btnSend = document.getElementById('btnConfirmSendEmail');

    let currentPreviewData = null;

    if (templateSelect) {
        templateSelect.addEventListener('change', function () {
            const templateName = this.value;
            const taskId = document.getElementById('emailTemplateTaskId').value;

            if (!templateName || !taskId) return;

            // Show loading
            previewContainer.classList.add('d-none');
            previewEmpty.classList.add('d-none');
            previewLoading.classList.remove('d-none');
            if (btnNext) btnNext.disabled = true;
            currentPreviewData = null;

            fetch(`/Communication/GetTemplatePreview?taskId=${taskId}&templateName=${encodeURIComponent(templateName)}`)
                .then(res => res.json())
                .then(data => {
                    if (!data.success) throw new Error(data.message || "Failed to load preview");

                    previewLoading.classList.add('d-none');
                    previewContainer.classList.remove('d-none');

                    // Write HTML to iframe
                    const doc = previewFrame.contentWindow.document;
                    doc.open();
                    doc.write(data.html);
                    doc.close();

                    currentPreviewData = data;
                    if (btnNext) btnNext.disabled = false;
                })
                .catch(err => {
                    console.error("Preview error:", err);
                    previewLoading.classList.add('d-none');
                    previewEmpty.classList.remove('d-none');
                    previewEmpty.innerHTML = `<i class="bi bi-exclamation-triangle text-danger display-4"></i><p class="text-danger mt-2">${err.message || 'Error loading preview'}</p>`;
                });
        });
    }

    if (btnNext) {
        btnNext.addEventListener('click', function () {
            if (!currentPreviewData) return;

            // Populate Step 2 confirmation details
            document.getElementById('confirmFromEmail').textContent = currentPreviewData.fromEmail || 'Unknown';
            document.getElementById('confirmToEmail').textContent = currentPreviewData.toEmail || 'Unknown';
            document.getElementById('confirmTemplateName').textContent = templateSelect.options[templateSelect.selectedIndex].text;

            // Switch UI to Step 2
            document.getElementById('emailStep1').classList.add('d-none');
            document.getElementById('footerStep1').classList.add('d-none');
            document.getElementById('emailStep2').classList.remove('d-none');
            document.getElementById('footerStep2').classList.remove('d-none');
        });
    }

    if (btnBack) {
        btnBack.addEventListener('click', function () {
            // Switch UI back to Step 1
            document.getElementById('emailStep2').classList.add('d-none');
            document.getElementById('footerStep2').classList.add('d-none');
            document.getElementById('emailStep1').classList.remove('d-none');
            document.getElementById('footerStep1').classList.remove('d-none');
        });
    }

    if (btnSend) {
        btnSend.addEventListener('click', function () {
            const taskId = document.getElementById('emailTemplateTaskId').value;
            const templateName = templateSelect.value;
            if (!taskId || !templateName) return;

            // Loading state
            const icon = document.getElementById('sendEmailIcon');
            const text = document.getElementById('sendEmailText');
            icon.className = 'spinner-border spinner-border-sm';
            text.textContent = 'Sending...';
            btnSend.disabled = true;
            if (btnBack) btnBack.disabled = true;

            const token = window.getAntiForgeryToken ? window.getAntiForgeryToken() : '';

            fetch(`/Communication/SendTemplateEmail?taskId=${taskId}&templateName=${encodeURIComponent(templateName)}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                }
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        if (typeof showToast === 'function') showToast(data.message, 'success');
                        const modalEl = document.getElementById('emailTemplateModal');
                        const modal = bootstrap.Modal.getInstance(modalEl);
                        if (modal) modal.hide();
                    } else {
                        if (typeof showToast === 'function') showToast(data.message || 'Failed to send email', 'danger');
                    }
                })
                .catch(err => {
                    console.error(err);
                    if (typeof showToast === 'function') showToast('An error occurred while sending', 'danger');
                })
                .finally(() => {
                    icon.className = 'bi bi-send-fill';
                    text.textContent = 'Confirm & Send Mail';
                    btnSend.disabled = false;
                    if (btnBack) btnBack.disabled = false;
                });
        });
    }
});

// End of tasks.js

// ═══════════════════════════════════════════════
// QUICK ASSIGN FUNCTIONS
// ═══════════════════════════════════════════════

function toggleQuickAssign() {
    const dropdown = document.getElementById('quickAssignDropdown');
    const searchContainer = document.getElementById('quickAssignSearchContainer');
    const searchInput = document.getElementById('quickAssignSearchInput');
    const btn = document.getElementById('btnQuickAssign');

    if (!dropdown || !btn || !searchContainer) return;

    const isHidden = searchContainer.classList.contains('d-none');

    if (isHidden) {
        // SHOW search bar
        searchContainer.classList.remove('d-none');
        // Small delay to allow transition after d-none removal
        setTimeout(() => searchContainer.classList.add('show'), 10);
        
        // SHOW dropdown (positioning handled by CSS absolute)
        dropdown.classList.add('show');
        filterQuickAssignList(true);
        setTimeout(() => {
            if (searchInput) {
                searchInput.focus();
                searchInput.setAttribute('autocomplete', 'off');
            }
        }, 100);
    } else {
        // HIDE everything
        searchContainer.classList.remove('show');
        // Wait for transition before adding d-none
        setTimeout(() => {
            searchContainer.classList.add('d-none');
            if (searchInput) searchInput.value = '';
            filterQuickAssignList(true);
        }, 250);
        dropdown.classList.remove('show');
    }
}

function filterQuickAssignList(showAll = false) {
    const searchInput = document.getElementById('quickAssignSearchInput');
    const search = searchInput ? searchInput.value.toLowerCase().trim() : '';
    const list = document.getElementById('quickAssignList');
    if (!list) return;

    const items = list.querySelectorAll('.quick-assign-item');
    const groups = list.querySelectorAll('.quick-assign-group');

    // Clear any existing empty state
    const existingEmpty = list.querySelector('.quick-assign-empty-state');
    if (existingEmpty) existingEmpty.remove();

    let totalVisible = 0;

    items.forEach(item => {
        const name = (item.dataset.name || '').toLowerCase();
        const role = (item.dataset.role || '').toLowerCase();
        const team = (item.dataset.team || '').toLowerCase();

        const visible = showAll || search.length === 0 || name.includes(search) || role.includes(search) || team.includes(search);

        item.style.display = visible ? 'block' : 'none';
        if (visible) totalVisible++;
    });

    // Hide/Show group headers based on item visibility
    groups.forEach(group => {
        let hasVisibleItems = false;
        let next = group.nextElementSibling;
        while (next && next.classList.contains('quick-assign-item')) {
            if (next.style.display !== 'none') {
                hasVisibleItems = true;
                break;
            }
            next = next.nextElementSibling;
        }
        group.style.display = hasVisibleItems ? 'block' : 'none';
    });

    if (totalVisible === 0 && search.length > 0) {
        const noResults = document.createElement('div');
        noResults.className = 'quick-assign-empty-state';
        noResults.innerHTML = '<i class="bi bi-person-x"></i>No results for "' + search + '"';
        list.appendChild(noResults);
    }
}

function selectQuickAssignUser(id, name, role, team) {
    const input = document.getElementById('taskAssignedToUserId');
    const teamInput = document.getElementById('taskAssignedToTeam');
    const btn = document.getElementById('btnQuickAssign');
    const dropdown = document.getElementById('quickAssignDropdown');
    const badge = document.getElementById('quickAssignSelectedUserBadge');
    const nameSpan = document.getElementById('quickAssignSelectedUserName');
    const searchContainer = document.getElementById('quickAssignSearchContainer');

    if (input) {
        input.value = id;
        input.setAttribute("data-assigned-name", name);
    }
    
    if (teamInput) teamInput.value = team;

    // Show selection badge
    if (badge && nameSpan) {
        nameSpan.textContent = name;
        badge.classList.remove('d-none');
        badge.classList.add('d-inline-flex');
    }

    // Hide search bar after selection
    if (searchContainer) {
        searchContainer.classList.remove('show');
        setTimeout(() => searchContainer.classList.add('d-none'), 250);
    }

    // Switch to selected state icon
    if (btn) {
        btn.innerHTML = `<i class="bi bi-person-check-fill fs-5"></i>`;
        btn.classList.remove('btn-outline-info');
        btn.classList.add('btn-info', 'text-white');
        btn.title = `Assigned to ${name}`;
    }

    // Close dropdown
    if (dropdown) dropdown.classList.remove('show');

    if (typeof showToast === 'function') showToast(`User ${name} selected! Click 'Create' to assign.`, 'info');
}

function clearQuickAssignSelection(event) {
    if (event) event.stopPropagation();
    
    const input = document.getElementById('taskAssignedToUserId');
    const teamInput = document.getElementById('taskAssignedToTeam');
    const badge = document.getElementById('quickAssignSelectedUserBadge');
    const btn = document.getElementById('btnQuickAssign');

    if (input) {
        input.value = '';
        input.removeAttribute("data-assigned-name");
    }
    if (teamInput) teamInput.value = '';
    
    if (badge) {
        badge.classList.add('d-none');
        badge.classList.remove('d-inline-flex');
    }

    if (btn) {
        btn.innerHTML = '<i class="bi bi-person-plus fs-5"></i>';
        btn.classList.add('btn-outline-info');
        btn.classList.remove('btn-info', 'text-white');
    }
}

// Close dropdown when clicking outside
document.addEventListener('mousedown', function (e) {
    const wrapper = e.target.closest('.quick-assign-wrapper');
    if (!wrapper) {
        const dropdown = document.getElementById('quickAssignDropdown');
        if (dropdown) dropdown.classList.remove('show');
    }
});

// Reset assignment and HIDE everything when modal opens
$(document).on('show.bs.modal', '#createTaskModal, #editTaskModal', function () {
    const isCreate = this.id === 'createTaskModal';
    const inputId = isCreate ? 'taskAssignedToUserId' : 'editTaskAssignedTo';
    const input = document.getElementById(inputId);
    const btnId = isCreate ? 'btnQuickAssign' : null; // Quick Assign only on Create for now? 
    // Actually the logic should be generic if btnQuickAssign is shared.
    
    const btn = document.getElementById('btnQuickAssign');
    const dropdown = document.getElementById('quickAssignDropdown');
    const searchContainer = document.getElementById('quickAssignSearchContainer');
    const searchInput = document.getElementById('quickAssignSearchInput');

    if (input && isCreate) {
        input.value = '';
        input.removeAttribute("data-assigned-name");
    }
    
    if (btn) {
        btn.innerHTML = '<i class="bi bi-person-plus fs-5"></i>';
        btn.classList.remove('btn-info', 'text-white');
        btn.classList.add('btn-outline-info');
        btn.title = 'Quick Assign';
    }
    
    if (searchContainer) {
        searchContainer.classList.remove('show');
        searchContainer.classList.add('d-none');
    }
    
    if (dropdown) dropdown.classList.remove('show');
    if (searchInput) searchInput.value = '';

    const badge = document.getElementById('quickAssignSelectedUserBadge');
    if (badge) {
        badge.classList.add('d-none');
        badge.classList.remove('d-inline-flex');
    }
    
    const teamInput = document.getElementById('taskAssignedToTeam');
    if (teamInput) teamInput.value = '';

    const list = document.getElementById('quickAssignList');
    if (list) {
        list.querySelectorAll('.quick-assign-item').forEach(i => i.style.display = 'none');
        list.querySelectorAll('.quick-assign-group').forEach(g => g.style.display = 'none');
    }
});

// ================= COMMUNICATION LOGS =================

let currentLogsTeam = '';

function openCommunicationLogsModal(teamName) {
    currentLogsTeam = teamName;
    const modalEl = document.getElementById('communicationLogsModal');
    if (!modalEl) return;

    document.getElementById('logTeamNameTitle').textContent = `(${teamName})`;

    const modal = new bootstrap.Modal(modalEl);
    modal.show();

    refreshCommunicationLogs();
}

function refreshCommunicationLogs() {
    if (!currentLogsTeam) return;

    const tbody = document.getElementById('communicationLogsTbody');
    if (!tbody) return;

    tbody.innerHTML = `
        <tr>
            <td colspan="5" class="text-center py-4 text-muted">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                Loading logs...
            </td>
        </tr>
    `;

    fetch(`/Communication/GetCommunicationLogs?teamName=${encodeURIComponent(currentLogsTeam)}`)
        .then(res => {
            if (!res.ok) throw new Error("Failed to fetch logs");
            return res.json();
        })
        .then(logs => {
            if (!logs || logs.length === 0) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="7" class="text-center py-4 text-muted">
                            <i class="bi bi-inbox fs-4 d-block mb-3"></i>
                            No communication logs found for this team.
                        </td>
                    </tr>
                `;
                return;
            }

            let html = '';
            logs.forEach(log => {
                const dateObj = new Date(log.sentAt);
                const dateStr = dateObj.toLocaleDateString() + ' ' + dateObj.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

                const actionBadge = log.action === 'WhatsApp Click'
                    ? '<span class="badge bg-success" style="font-size: 0.8rem;"><i class="bi bi-whatsapp me-1"></i> WhatsApp</span>'
                    : '<span class="badge bg-primary" style="font-size: 0.8rem;"><i class="bi bi-envelope me-1"></i> Email</span>';


                const statusBadge = log.status === 'Sent'
                    ? '<span class="badge bg-success bg-opacity-75"><i class="bi bi-check-circle me-1"></i>Sent</span>'
                    : log.status === 'Failed'
                        ? '<span class="badge bg-danger bg-opacity-75"><i class="bi bi-x-circle me-1"></i>Failed</span>'
                        : `<span class="badge bg-secondary bg-opacity-75">${log.status || '-'}</span>`;

                html += `
                    <tr>
                        <td class="text-muted small">${dateStr}</td>
                        <td class="fw-bold text-dark">${log.leadName || '-'}</td>
                        <td>${actionBadge}</td>
                        <td class="small text-truncate" style="max-width: 150px;" title="${log.fromInfo || ''}">${log.fromInfo || '-'}</td>
                        <td class="small text-truncate" style="max-width: 150px;" title="${log.toInfo || ''}">${log.toInfo || '-'}</td>
                        <td>${statusBadge}</td>
                        <td class="small fw-medium"><i class="bi bi-person me-1 text-secondary"></i>${log.user || '-'}</td>
                    </tr>
                `;
            });

            tbody.innerHTML = html;
        })
        .catch(err => {
            console.error("Error fetching logs:", err);
            tbody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center py-4 text-danger">
                        <i class="bi bi-exclamation-triangle me-2"></i> Failed to load logs.
                    </td>
                </tr>
            `;
            if (typeof showToast === 'function') showToast("Failed to load communication logs", "danger");
        });
}

// ================= PER-TASK COMMUNICATION LOGS =================

let currentCommLogsTaskId = null;

function openTaskCommLogsModal(taskId) {
    currentCommLogsTaskId = taskId;
    const modalEl = document.getElementById('taskCommLogsModal');
    if (!modalEl) return;

    document.getElementById('taskCommLogTitle').textContent = `(Task #${taskId})`;

    const modal = new bootstrap.Modal(modalEl);
    modal.show();

    refreshTaskCommLogs();
}

function refreshTaskCommLogs() {
    if (!currentCommLogsTaskId) return;

    const tbody = document.getElementById('taskCommLogsTbody');
    if (!tbody) return;

    tbody.innerHTML = `
        <tr>
            <td colspan="6" class="text-center py-4 text-muted">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                Loading logs...
            </td>
        </tr>
    `;

    fetch(`/Communication/GetTaskCommunicationLogs?taskId=${currentCommLogsTaskId}`)
        .then(res => {
            if (!res.ok) throw new Error("Failed to fetch logs");
            return res.json();
        })
        .then(logs => {
            if (!logs || logs.length === 0) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="6" class="text-center py-4 text-muted">
                            <i class="bi bi-inbox fs-4 d-block mb-3"></i>
                            No communication logs found for this task.
                        </td>
                    </tr>
                `;
                return;
            }

            let html = '';
            logs.forEach(log => {
                const dateObj = new Date(log.sentAt);
                const dateStr = dateObj.toLocaleDateString() + ' ' + dateObj.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

                const actionBadge = log.action === 'WhatsApp Click'
                    ? '<span class="badge bg-success" style="font-size: 0.8rem;"><i class="bi bi-whatsapp me-1"></i> WhatsApp</span>'
                    : '<span class="badge bg-primary" style="font-size: 0.8rem;"><i class="bi bi-envelope me-1"></i> Email</span>';

                const statusBadge = log.status === 'Sent'
                    ? '<span class="badge bg-success bg-opacity-75"><i class="bi bi-check-circle me-1"></i>Sent</span>'
                    : log.status === 'Failed'
                        ? '<span class="badge bg-danger bg-opacity-75"><i class="bi bi-x-circle me-1"></i>Failed</span>'
                        : `<span class="badge bg-secondary bg-opacity-75">${log.status || '-'}</span>`;

                html += `
                    <tr>
                        <td class="text-muted small">${dateStr}</td>
                        <td>${actionBadge}</td>
                        <td class="small text-truncate" style="max-width: 150px;" title="${log.fromInfo || ''}">${log.fromInfo || '-'}</td>
                        <td class="small text-truncate" style="max-width: 150px;" title="${log.toInfo || ''}">${log.toInfo || '-'}</td>
                        <td>${statusBadge}</td>
                        <td class="small fw-medium"><i class="bi bi-person me-1 text-secondary"></i>${log.user || '-'}</td>
                    </tr>
                `;
            });

            tbody.innerHTML = html;
        })
        .catch(err => {
            console.error("Error fetching task logs:", err);
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center py-4 text-danger">
                        <i class="bi bi-exclamation-triangle me-2"></i> Failed to load logs.
                    </td>
                </tr>
            `;
        });
}
