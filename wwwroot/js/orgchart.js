
// ===============================
// CSRF TOKEN HELPER (REQUIRED)
// ===============================
function getCsrfToken() {
    const tokenInput = document.querySelector(
        'input[name="__RequestVerificationToken"]'
    );
    return tokenInput ? tokenInput.value : '';
}

window.drag = function (ev) {
    console.log("🖱️ DRAG ATTEMPT START");
    if (!window.__isAdmin && !window.__isManager) {
        console.warn("🚫 DRAG BLOCKED: User is not Admin/Manager");
        return;
    }

    // Use data-id selector instead of .org-node
    const node = ev.target.closest("[data-id]");
    if (!node) {
        console.warn("🚫 DRAG BLOCKED: No [data-id] parent found");
        return;
    }

    const draggedNode = {
        id: node.dataset.id,
        role: node.dataset.role,
        fromParent: node.dataset.parentId || "ADMIN",
        name: node.querySelector(".node-name")?.innerText?.trim() // Fixed selector to .node-name
    };

    console.log("✅ DRAG STARTED:", draggedNode);

    ev.dataTransfer.effectAllowed = "move";
    ev.dataTransfer.setData(
        "application/json",
        JSON.stringify(draggedNode)
    );
};

window.allowDrop = function (ev) {
    if (!window.__isAdmin && !window.__isManager) return;
    ev.preventDefault(); // REQUIRED
};

window.drop = function (event, newParentId) {
    console.log("🖱️ DROP ATTEMPT START on target:", newParentId);
    event.preventDefault();
    if (!window.__isAdmin && !window.__isManager) {
        console.warn("🚫 DROP BLOCKED: User is not Admin/Manager");
        return;
    }

    const data = event.dataTransfer.getData("application/json");
    if (!data) {
        console.warn("🚫 DROP BLOCKED: No data in dataTransfer");
        return;
    }

    const dragged = JSON.parse(data);
    console.log("⬇️ DROP PROCEEDING:", dragged, "→", newParentId);

    // ❌ prevent self-drop
    if (dragged.id === newParentId) return;

    fetch('/Users/MoveOrgNode', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getCsrfToken()
        },
        body: JSON.stringify({
            userId: dragged.id,
            newParentId: newParentId
        })
    })
        .then(r => r.json())
        .then(res => {
            if (res.success) location.reload();
        })
        .catch(err => console.error(err));
};

(function () {

    if (window.__orgchart_loaded) return;
    window.__orgchart_loaded = true;

    // Private drag state
    let __draggedUserId = null;

    function showToast(text, type = 'success', timeout = 4500) {
        // Create container if not present
        let container = document.querySelector('.toast-container-fixed');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container-fixed';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `alert alert-${type} toast-message`;
        toast.textContent = text;
        container.appendChild(toast);

        setTimeout(() => {
            toast.classList.add('fade');
            setTimeout(() => toast.remove(), 400);
        }, timeout);
    }

    // Utility: inject HTML into container and execute inline scripts contained in the HTML
    function injectHtmlWithScripts(container, html) {
        const tmp = document.createElement('div');
        tmp.innerHTML = html;

        // Collect scripts
        const scripts = Array.from(tmp.querySelectorAll('script'));

        // Remove script elements from tmp to avoid duplication when setting innerHTML
        scripts.forEach(s => s.parentNode && s.parentNode.removeChild(s));

        // Inject non-script HTML
        container.innerHTML = tmp.innerHTML;

        // Execute scripts in order (external scripts will load, inline scripts will run immediately)
        scripts.forEach(s => {
            const newScript = document.createElement('script');
            if (s.src) {
                newScript.src = s.src;
                if (s.async) newScript.async = true;
                if (s.defer) newScript.defer = true;
                document.head.appendChild(newScript);
            } else {
                newScript.text = s.textContent;
                document.body.appendChild(newScript);
                document.body.removeChild(newScript);
            }
        });

        if (typeof initializeUserReportsPanel === 'function') {
            try { initializeUserReportsPanel(); } catch { /* ignore */ }
        }
        if (typeof window.__org_bind_modals === 'function') {
            try { window.__org_bind_modals(); } catch { /* ignore */ }
        }
    }



    // ---------------- Load reports ----------------
    window.loadReports = function (userId) {
        try {
            const panel = document.getElementById("detailsPanel");
            if (!panel) {
                console.warn("detailsPanel not found");
                return;
            }

            panel.dataset.userid = userId || "";
            panel.innerHTML = "<div class='text-muted'>Loading reports...</div>";

            fetch(`/Reports/UserReportsPanel?userId=${encodeURIComponent(userId)}`)
                .then(r => { if (!r.ok) throw r; return r.text(); })
                .then(html => {
                    injectHtmlWithScripts(panel, html);
                })
                .catch(async err => {
                    console.error("loadReports error", err);
                    let msg = "Failed to load reports";
                    if (err instanceof Response) {
                        try { msg = await err.text(); } catch { /* ignore */ }
                    }
                    panel.innerHTML = `<div class='text-danger'>${msg}</div>`;
                });
        } catch (ex) {
            console.error("loadReports error", ex);
        }
    };

    // ---------------- Show inline add report ----------------
    window.showInlineAddReport = function (userId) {
        const dateInput = document.getElementById("addReportDate");
        if (dateInput && dateInput.value) {
            const [y, m, d] = dateInput.value.split("-");
            const formatted = `${d}-${m}-${y}`;
            fetch(`/Reports/CheckReportExists?userId=${encodeURIComponent(userId)}&date=${encodeURIComponent(formatted)}`)
                .then(r => r.json())
                .then(data => {
                    if (data.exists) {
                        alert(`Report already exists for ${formatted}`);
                        return;
                    }
                    loadAddReportForm(userId, formatted);
                })
                .catch(() => alert("Failed to check existing report"));
            return;
        }

        loadAddReportForm(userId, new Date().toLocaleDateString('en-GB').split('/').reverse().join('-'));
    };

    window.loadAddReportForm = function (userId, formattedDate) {
        const container = document.getElementById("inlineAddReportContainer") || document.getElementById("detailsPanel");
        if (!container) return;

        container.classList.remove("d-none");
        container.innerHTML = "<div class='text-muted p-3'>Loading add form...</div>";

        fetch(`/Reports/AddReportPanel?userId=${encodeURIComponent(userId)}&date=${encodeURIComponent(formattedDate)}`)
            .then(r => { if (!r.ok) throw r; return r.text(); })
            .then(html => {
                injectHtmlWithScripts(container, html);
            })
            .catch(async err => {
                console.error("loadAddReportForm error", err);
                let txt = "Failed to load add form";
                if (err instanceof Response) {
                    try { txt = await err.text(); } catch { /* ignore */ }
                }
                container.innerHTML = `<div class='text-danger p-3'>${txt}</div>`;
            });
    };

    window.openAddReportInline = window.showInlineAddReport;

    // ---------------- Modal helpers (same as before) ----------------
    window.openEditModal = function (id, name, email, role) {
        try {
            const editModalEl = document.getElementById('editUserModal');
            if (!editModalEl) { location.href = `/Users/Index?editId=${encodeURIComponent(id)}`; return; }

            const editId = document.getElementById('editId');
            const editRole = document.getElementById('editRole');
            const editName = document.getElementById('editName');
            const editEmail = document.getElementById('editEmail');
            const originalName = document.getElementById('originalName');
            const originalEmail = document.getElementById('originalEmail');
            if (editId) editId.value = id ?? '';
            if (editRole) editRole.value = role ?? '';
            if (editName) editName.value = name ?? '';
            if (editEmail) editEmail.value = email ?? '';
            if (originalName) originalName.value = name ?? '';
            if (originalEmail) originalEmail.value = email ?? '';

            new bootstrap.Modal(editModalEl).show();
        } catch (ex) { console.error("openEditModal", ex); }
    };

    window.openDeleteModal = function (id, name, role) {
        try {
            const deleteModalEl = document.getElementById('deleteModal');
            if (!deleteModalEl) {
                if (!confirm(`Delete ${name} (${role})?`)) return;
                fetch('/Users/DeleteUser', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: new URLSearchParams({ id, reassignToId: '' }).toString()
                }).then(r => { if (!r.ok) r.text().then(t => alert(t)); else location.reload(); });
                return;
            }

            const deleteId = document.getElementById('deleteId');
            const deleteName = document.getElementById('deleteName');
            const deleteRole = document.getElementById('deleteRole');
            const deleteConfirmText = document.getElementById('deleteConfirmText');
            const reassignBlock = document.getElementById('reassignBlock');
            const reassignSelect = document.getElementById('reassignManager');

            if (deleteId) deleteId.value = id ?? '';
            if (deleteName) deleteName.value = name ?? '';
            if (deleteRole) deleteRole.value = role ?? '';
            if (deleteConfirmText) deleteConfirmText.innerText = `Are you sure you want to delete ${name} (${role})?`;
            if (reassignBlock) {
                if (role === "Manager") reassignBlock.classList.remove('d-none'); else reassignBlock.classList.add('d-none');
            }

            // Populate reassign select dynamically with manager/submanager nodes found on page (exclude current id)
            if (reassignSelect) {
                // Clear existing options
                reassignSelect.innerHTML = '';

                // Add Admin option
                const adminOpt = document.createElement('option');
                adminOpt.value = 'ADMIN';
                adminOpt.text = 'Admin';
                reassignSelect.appendChild(adminOpt);

                // Find manager and submanager nodes in DOM (data-role Manager/SubManager)
                const nodes = Array.from(document.querySelectorAll('[data-role="Manager"], [data-role="SubManager"]'));
                const added = new Set();

                nodes.forEach(n => {
                    const nid = n.dataset.id;
                    const titleEl = n.querySelector('.node-title') || n.querySelector('.manager-text .node-title');
                    const label = (titleEl && titleEl.textContent && titleEl.textContent.trim()) || n.dataset.name || `Manager ${nid}`;
                    if (!nid || nid === id) return; // exclude the one being deleted
                    if (added.has(nid)) return;
                    added.add(nid);
                    const opt = document.createElement('option');
                    opt.value = nid;
                    opt.text = label;
                    reassignSelect.appendChild(opt);
                });
            }

            new bootstrap.Modal(deleteModalEl).show();
        } catch (ex) { console.error("openDeleteModal", ex); }
    };

    window.openAddModal = function (role, managerId) {
        try {
            const addModalEl = document.getElementById('addUserModal');
            if (!addModalEl) { location.href = '/Users/Create'; return; }
            const roleInput = document.getElementById('role');
            const managerInput = document.getElementById('managerId');
            const modalTitle = document.getElementById('modalTitle');
            const addUserForm = document.getElementById('addUserForm');
            const addBtn = document.getElementById('addBtn');
            const nameInput = document.getElementById('nameInput');

            // Reset form first (clears previous values), then set hidden inputs
            if (addUserForm) addUserForm.reset();
            // 🔴 FIX #2: Explicitly clear team checkboxes
            document.querySelectorAll('.team-checkbox').forEach(cb => {
                cb.checked = false;
            });


            if (roleInput) roleInput.value = role ?? '';
            if (managerInput) managerInput.value = managerId ?? '';
            // Set modal title depending on role/managerId; admin root and manager nodes handled in radio logic
            if (managerId && managerId.toUpperCase() === 'ADMIN') {
                // admin root - default title
                if (modalTitle) modalTitle.innerText = 'Add User / Manager';
            } else if (role === 'Manager') {
                if (modalTitle) modalTitle.innerText = 'Add Manager';
            } else {
                if (modalTitle) modalTitle.innerText = 'Add User';
            }

            // Load teams dynamically
            loadTeamCheckboxes();

            // Ensure second radio label/value reflect context:
            const secondRadio = document.getElementById('addSecondType');
            const secondLabel = document.getElementById('addSecondLabel');
            const userRadio = document.getElementById('addUserType');

            if (managerId && managerId.toUpperCase() === 'ADMIN') {
                // Admin root: show "Manager" as second option (creates top-level manager)
                if (secondRadio) secondRadio.value = 'Manager';
                if (secondLabel) secondLabel.textContent = 'Manager';
            } else {
                // Manager / SubManager nodes: second option is "Sub Manager"
                if (secondRadio) secondRadio.value = 'SubManager';
                if (secondLabel) secondLabel.textContent = 'Sub Manager';
            }

            // Helper declared here so it's available when openAddModal calls it.
            function updateTitleAndHiddenRole() {
                try {
                    const addType = addModalEl.querySelector('input[name="addType"]:checked')?.value ?? 'User';
                    if (addType === 'Manager') {
                        if (modalTitle) modalTitle.innerText = 'Add Manager';
                        if (roleInput) roleInput.value = 'Manager';
                    } else if (addType === 'SubManager') {
                        if (modalTitle) modalTitle.innerText = 'Add Sub Manager';
                        // keep roleInput empty — create flow uses addType to determine manager role
                        if (roleInput) roleInput.value = '';
                    } else {
                        if (modalTitle) modalTitle.innerText = 'Add User';
                        if (roleInput) roleInput.value = '';
                    }
                } catch (e) { /* ignore */ }
            }

            // initial title set
            updateTitleAndHiddenRole();

            // ensure Add button disabled until valid inputs provided
            if (addBtn) addBtn.disabled = true;

            if (addUserForm && typeof addUserForm.reportValidity === 'function') {
                // clear validation UI if any
                addUserForm.classList.remove('was-validated');
            }

            // bind radios (only once is fine)
            Array.from(addModalEl.querySelectorAll('input[name="addType"]')).forEach(r => {
                r.removeEventListener('change', updateTitleAndHiddenRole);
                r.addEventListener('change', updateTitleAndHiddenRole);
            });

            new bootstrap.Modal(addModalEl).show();

            // focus first field after modal shows
            setTimeout(() => {
                if (nameInput) nameInput.focus();
            }, 100);
        } catch (ex) { console.error("openAddModal", ex); }
    };

    // ---------------- Bind modal forms if present ----------------
    function bindModalForms() {
        // Add user inline (AJAX)
        const addForm = document.getElementById('addUserForm');
        if (addForm && !addForm.dataset.bound) {
            addForm.dataset.bound = "1";

            const addBtn = document.getElementById('addBtn');
            const nameInput = document.getElementById('nameInput');
            const emailInput = document.getElementById('emailInput');

            function validateAddForm() {
                try {
                    const name = nameInput?.value?.trim() ?? '';
                    const email = emailInput?.value?.trim() ?? '';
                    const emailValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
                    if (addBtn) addBtn.disabled = !(name && email && emailValid);
                } catch (e) { if (addBtn) addBtn.disabled = true; }
            }

            // wire inputs to validate
            nameInput?.addEventListener('input', validateAddForm);
            emailInput?.addEventListener('input', validateAddForm);

            // initial validation state
            validateAddForm();

            addForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                const name = document.getElementById('nameInput')?.value?.trim() ?? '';
                const email = document.getElementById('emailInput')?.value?.trim() ?? '';
                const role = document.getElementById('role')?.value ?? '';
                const managerId = document.getElementById('managerId')?.value ?? '';

                const addType = document.querySelector('input[name="addType"]:checked')?.value ?? 'User';

                // 🔹 STEP 1: Collect selected teams (Development / Testing / Sales)
                const selectedTeams = Array.from(
                    document.querySelectorAll('.team-checkbox:checked')
                ).map(cb => cb.value);



                console.log("🚨 SUBMIT STARTED");
                console.log("📦 Selected teams:", selectedTeams);


                // ❗ REQUIRED: Ensure at least one team is selected
                if (selectedTeams.length === 0) {
                    alert("Please assign at least one team.");
                    return;
                }





                if (!name || !email) { alert('Name and Email required'); return; }

                const fd = new URLSearchParams();
                fd.append('name', name);
                fd.append('email', email);
                fd.append('role', role);
                fd.append('managerId', managerId);
                fd.append('addType', addType);
                selectedTeams.forEach(team => {
                    fd.append('teams', team);
                });

                const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenEl) fd.append('__RequestVerificationToken', tokenEl.value);

                const res = await fetch('/Users/CreateInline', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: fd.toString()
                });

                const txt = await res.text();
                if (!res.ok) { alert(txt || 'Failed to create'); return; }

                location.reload();
            });
        }

        // Edit user inline (AJAX)
        const editForm = document.getElementById('editForm');
        if (editForm && !editForm.dataset.bound) {
            editForm.dataset.bound = "1";
            editForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                const id = document.getElementById('editId')?.value;
                const name = document.getElementById('editName')?.value?.trim();
                const email = document.getElementById('editEmail')?.value?.trim();
                if (!id || !name || !email) { alert('Invalid data'); return; }

                const fd = new URLSearchParams();
                fd.append('id', id);
                fd.append('name', name);
                fd.append('email', email);

                const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenEl) fd.append('__RequestVerificationToken', tokenEl.value);

                const res = await fetch('/Users/EditInline', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: fd.toString()
                });

                const txt = await res.text();
                if (!res.ok) { alert(txt || 'Failed to update'); return; }
                location.reload();
            });
        }

        // Delete user inline (AJAX)
        const deleteForm = document.getElementById('deleteForm');
        if (deleteForm && !deleteForm.dataset.bound) {
            deleteForm.dataset.bound = "1";
            deleteForm.addEventListener('submit', async function (e) {
                e.preventDefault();
                const id = document.getElementById('deleteId')?.value;
                let reassignToId = document.getElementById('reassignManager')?.value ?? '';
                if (!id) { alert('Invalid id'); return; }

                const fd = new URLSearchParams();
                fd.append('id', id);
                fd.append('reassignToId', reassignToId);

                const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenEl) fd.append('__RequestVerificationToken', tokenEl.value);

                const res = await fetch('/Users/DeleteUser', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: fd.toString()
                });

                const txt = await res.text();
                if (!res.ok) { alert(txt || 'Failed to delete'); return; }
                location.reload();
            });
        }
    }

    // Expose bind function so injected partials / re-initializers can call it
    window.__org_bind_modals = bindModalForms;

    // Run immediately on load (if DOM is ready)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindModalForms);
    } else {
        bindModalForms();
    }

    document.addEventListener('DOMContentLoaded', function () {
        try { if (typeof window.__org_bind_modals === 'function') window.__org_bind_modals(); } catch { }
    });

    // Helper to load teams
    function loadTeamCheckboxes() {
        const container = document.getElementById('teamCheckboxesContainer');
        if (!container) return;

        container.innerHTML = '<div class="text-muted small">Loading...</div>';

        fetch('/Teams/GetAll')
            .then(res => res.json())
            .then(teams => {
                if (teams.length === 0) {
                    container.innerHTML = '<div class="text-muted small">No teams defined</div>';
                    return;
                }

                container.innerHTML = teams.map(t => `
                    <div class="form-check">
                        <input class="form-check-input team-checkbox"
                               type="checkbox"
                               name="teams"
                               value="${t.name}"
                               id="team_${t.id}" />
                        <label class="form-check-label" for="team_${t.id}">
                            ${t.name} Team
                        </label>
                    </div>
                `).join('');
            })
            .catch(() => {
                container.innerHTML = '<div class="text-danger small">Failed to load teams</div>';
            });
    }

})();



// ===============================
// ADD REPORT SUBMIT (DELEGATED)
// ===============================
// ===============================
// ADD REPORT SUBMIT (DELEGATED - FINAL)
// ===============================
document.addEventListener("click", async function (e) {

    const btn = e.target.closest("#submitReportBtn");
    if (!btn) return;

    const container = btn.closest("#addReportFormContainer");
    if (!container) {
        console.error("❌ AddReportFormContainer not found");
        return;
    }

    const targetUserId = container.querySelector('input[name="targetUserId"]')?.value;
    const date = container.querySelector('input[name="date"]')?.value;
    const task = container.querySelector('textarea[name="task"]')?.value.trim();
    const note = container.querySelector('textarea[name="note"]')?.value.trim();

    const reportedTo = Array.from(
        container.querySelectorAll('input[name="reportedTo"]:checked')
    ).map(x => x.value);

    if (!targetUserId || !date || !task || !note || reportedTo.length === 0) {
        alert("Please fill all fields and select Reported To");
        return;
    }

    console.log("✅ Submitting report:", {
        targetUserId, date, task, note, reportedTo
    });

    const formData = new FormData();
    formData.append("targetUserId", targetUserId);
    formData.append("date", date);
    formData.append("task", task);
    formData.append("note", note);
    reportedTo.forEach(r => formData.append("reportedTo", r));

    try {
        const res = await fetch("/Reports/CreateInline", {
            method: "POST",
            body: formData,
            credentials: "same-origin"
        });

        const text = await res.text();

        if (!res.ok) {
            console.error("❌ CreateInline failed:", text);
            alert(text);
            return;
        }

        const result = JSON.parse(text);

        if (result.success) {
            console.log("✅ Report created");
            loadReports(result.userId);
        }
    }
    catch (err) {
        console.error("❌ Submit error:", err);
        alert("Unexpected error submitting report");
    }
});
