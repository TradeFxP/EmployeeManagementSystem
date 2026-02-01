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

    const targetStatus = $(this).data("status");

    if (!isMoveAllowed(draggedFromStatus, targetStatus)) {
        alert("You are not allowed to move this task.");
        return;
    }

    $.post("/Tasks/UpdateStatus", {
        taskId: draggedTaskId,
        newStatus: targetStatus
    })
        .done(() => {
            location.reload();
        })
        .fail(() => {
            alert("Status update failed");
        });
});



function isMoveAllowed(from, to) {

    // SAME COLUMN
    if (from === to) return false;

    const role = document.body.dataset.role; // we’ll add this next

    if (role === "User") {
        return (
            (from === "ToDo" && to === "Doing") ||
            (from === "Doing" && to === "Review")
        );
    }

    // Admin / Manager / Sub-Manager
    return (
        (from === "ToDo" && to === "Doing") ||
        (from === "Doing" && to === "Review") ||
        (from === "Review" && to === "Complete")
    );
}


$(document).on("dragenter", ".kanban-column", function () {
    $(this).addClass("drag-over");
});

$(document).on("dragleave drop", ".kanban-column", function () {
    $(this).removeClass("drag-over");
});
