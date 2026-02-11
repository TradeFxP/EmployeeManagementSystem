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

            // Reload current board
            $(".task-link.active").click();
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

    if (!confirm("Delete this task?")) return;

    const id = $(this).data("id");

    $.post("/Tasks/DeleteTask", { id: id })
        .done(() => location.reload())
        .fail(() => alert("Delete failed"));
});



$(document).on("submit", "#addTaskForm", function (e) {
    e.preventDefault();

    $.ajax({
        url: "/Tasks/CreateTask",
        type: "POST",
        data: $(this).serialize(),
        success: function () {
            $("#addTaskModal").modal("hide");

            // reload current board
            $(".task-link.active").click();
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
    }).done(() => location.reload());
});



let draggedTaskId = null;
let draggedFromStatus = null;

// ================= DRAG START =================
$(document).on("dragstart", ".task-card", function (e) {
    draggedTaskId = $(this).data("task-id");
    draggedFromStatus = $(this).data("current-status");

    e.originalEvent.dataTransfer.effectAllowed = "move";
});

// ================= ALLOW DROP =================
$(document).on("dragover", ".kanban-column", function (e) {
    e.preventDefault(); // REQUIRED
});

// ================= DROP =================
$(document).on("drop", ".kanban-column", function (e) {
    e.preventDefault();

    // 🔥 IGNORE IF NOT DRAGGING A TASK (e.g. column drag)
    if (!draggedTaskId) return;

    const targetStatus = $(this).data("status");



    // $.post("/Tasks/UpdateStatus", {
    //     taskId: draggedTaskId,
    //     newStatus: targetStatus
    // })
    //     .done(() => {
    //         location.reload();
    //     })
    //     .fail(() => {
    //         alert("Status update failed");
    //     });
});



function isMoveAllowed(from, to) {
    if (from === to) return false;
    return true; // Allow all moves
}


$(document).on("dragenter", ".kanban-column", function () {
    $(this).addClass("drag-over");
});

$(document).on("dragleave drop", ".kanban-column", function () {
    $(this).removeClass("drag-over");
});

// ========== NEW TASK CREATION WITH PRIORITY & CUSTOM FIELDS ==========

// Open create task modal
async function openCreateTaskModal(columnId) {
    document.getElementById("taskColumnId").value = columnId;
    document.getElementById("taskTitle").value = "";
    document.getElementById("taskDescription").value = "";
    document.getElementById("taskPriority").value = "1"; // Default to Medium

    // Render custom fields if available and wait for them to load
    if (typeof renderCustomFieldInputs === 'function') {
        try {
            await renderCustomFieldInputs('customFieldsContainer');
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

    if (!title) {
        alert("Please enter a task title");
        return;
    }

    if (!columnId) {
        alert("Column ID is missing");
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
            customFieldValues: customFieldValues
        }),
        success: function (response) {
            if (response && response.success) {
                bootstrap.Modal.getInstance(document.getElementById("createTaskModal"))?.hide();
                // reload board
                if (typeof currentTeamName !== 'undefined') {
                    // prefer partial reload
                    if (window.loadTeamBoard) loadTeamBoard(currentTeamName);
                    else location.reload();
                } else {
                    location.reload();
                }
            } else {
                bootstrap.Modal.getInstance(document.getElementById("createTaskModal"))?.hide();
                // still reload to pick up new task
                if (typeof currentTeamName !== 'undefined' && window.loadTeamBoard) loadTeamBoard(currentTeamName);
                else location.reload();
            }
        },
        error: function (xhr) {
            const text = xhr.responseText || 'An error occurred while creating the task.';
            alert(text);
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
    // Hide actions toolbar
    $(`.task-card[data-task-id="${taskId}"] .task-actions`).addClass('d-none');

    // Show assign container
    $(`.task-assign-container[data-task-id="${taskId}"]`).removeClass('d-none');
}

function cancelAssignTask(taskId) {
    // Hide assign container
    $(`.task-assign-container[data-task-id="${taskId}"]`).addClass('d-none');

    // Show actions toolbar
    $(`.task-card[data-task-id="${taskId}"] .task-actions`).removeClass('d-none');
}

function confirmAssignTask(taskId) {
    const container = $(`.task-assign-container[data-task-id="${taskId}"]`);
    const select = container.find('.task-assign-select');
    const userId = select.val();

    if (!userId) {
        alert("Please select a user");
        return;
    }

    $.post("/Tasks/AssignTask", { taskId: taskId, userId: userId })
        .done(function (response) {
            if (response.success) {
                // Determine if we need to reload the whole board or just update UI
                // For simplicity, reload current board to reflect all changes (audit log, etc)
                const activeTeam = $(".task-link.active").data("url"); // e.g., /Tasks/TeamBoard?team=...
                if (activeTeam) {
                    $("#taskBoardContainer").load(activeTeam);
                } else {
                    location.reload();
                }
            } else {
                alert("Failed to assign task");
            }
        })
        .fail(function () {
            alert("Error assigning task");
        });
}
