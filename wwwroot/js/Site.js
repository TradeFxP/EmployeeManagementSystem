/* ================= ADD USER FORM (SAFE PLACEHOLDER) ================= */
document.addEventListener("DOMContentLoaded", function () {

    const addForm = document.getElementById("addUserForm");
    const addBtn = document.getElementById("addBtn");

    if (addForm) {
        addForm.addEventListener("submit", function (e) {
            e.preventDefault();
            if (addBtn && addBtn.disabled) return;
            // Actual submit handled in OrgChart.cshtml
        });
    }

    /* ================= AUTO DISMISS ALERTS ================= */
    document.querySelectorAll(".auto-dismiss").forEach(alert => {
        setTimeout(() => {
            alert.classList.add("fade");
            setTimeout(() => alert.remove(), 400);
        }, 5000);
    });

    /* ================= HASH SCROLL ================= */
    if (window.location.hash) {
        const target = document.querySelector(window.location.hash);
        if (target) {
            target.scrollIntoView({ behavior: "smooth" });
        }
    }
});

/* ================= TEXTAREA COUNTER ================= */
function updateCounter(textarea) {
    const counter = textarea.nextElementSibling;
    if (!counter) return;
    counter.textContent = textarea.maxLength - textarea.value.length;
}

document.addEventListener("input", function (e) {
    if (!e.target.classList.contains("char-limit")) return;

    const max = 100;
    const counterId = e.target.getAttribute("data-counter");
    const counter = document.getElementById(counterId);

    if (!counter) return;

    const remaining = max - e.target.value.length;
    counter.textContent = remaining + " characters remaining";
});


function selectNode(id) {
    fetch(`/Users/GetDetails?id=${id}`)
        .then(r => r.text())
        .then(html => {
            document.getElementById("detailsPanel").innerHTML = html;
        });
}

// Removed conflicting global drag/drop handlers so orgchart.js can handle DnD consistently.
// ================= EMPLOYEE MANAGEMENT AJAX =================
window.loadEmployeesPanel = function (searchTerm = "") {
    const container = document.getElementById("taskBoardContainer");
    if (!container) {
        // If not on Tasks/Index, redirect
        window.location.href = `/Tasks/Index?loadEmployees=true&search=${encodeURIComponent(searchTerm)}`;
        return;
    }

    container.innerHTML = '<div class="text-center py-5 text-muted"><div class="spinner-border mb-3" role="status"></div><br>Loading employees...</div>';

    fetch(`/Users/ListEmployees?search=${encodeURIComponent(searchTerm)}`)
        .then(res => res.text())
        .then(html => {
            container.innerHTML = html;
        })
        .catch(err => {
            console.error(err);
            container.innerHTML = '<div class="alert alert-danger m-4">Failed to load employee management panel.</div>';
        });
};

window.updateUserPermission = function (userId, permission, value) {
    const fd = new FormData();
    fd.append("userId", userId);
    fd.append("permission", permission);
    fd.append("value", value);

    fetch('/Users/UpdatePermission', {
        method: 'POST',
        headers: {
            'RequestVerificationToken': getCsrfToken()
        },
        body: fd
    }).then(res => {
        if (!res.ok) {
            alert("Failed to update permission");
            // location.reload();
        }
    }).catch(err => {
        console.error(err);
        alert("Error updating permission");
    });
};

window.deleteUserSimple = function (userId) {
    if (!confirm("Are you sure you want to delete this user?")) return;

    const fd = new URLSearchParams();
    fd.append("id", userId);
    fd.append("reassignToId", "");

    fetch('/Users/DeleteUser', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': getCsrfToken()
        },
        body: fd.toString()
    }).then(res => {
        if (!res.ok) {
            res.text().then(t => alert(t));
        } else {
            loadEmployeesPanel();
        }
    });
};
// ================= ADMIN: EMPLOYEE TEAM MANAGEMENT =================
window.loadEmployeesAdminPanel = function () {
    const container = document.getElementById("taskBoardContainer");
    if (!container) return;

    container.innerHTML = '<div class="text-center py-5 text-muted"><div class="spinner-border mb-3" role="status"></div><br>Loading employee list...</div>';

    fetch('/Users/ListEmployeesForAdmin')
        .then(res => {
            if (!res.ok) throw new Error("Failed to load");
            return res.text();
        })
        .then(html => {
            container.innerHTML = html;
        })
        .catch(err => {
            console.error(err);
            container.innerHTML = '<div class="alert alert-danger m-4">Failed to load employee management.</div>';
        });
};

window.updateUserTeamAssignment = function (userId, teamName, isAssigned) {
    const fd = new FormData();
    fd.append("userId", userId);
    fd.append("teamName", teamName);
    fd.append("isAssigned", isAssigned);

    fetch('/Users/UpdateUserTeams', {
        method: 'POST',
        headers: {
            'RequestVerificationToken': getCsrfToken()
        },
        body: fd
    }).then(res => {
        if (!res.ok) {
            alert("Failed to update team assignment");
        }
    }).catch(err => {
        console.error(err);
        alert("Error updating team assignment");
    });
};
