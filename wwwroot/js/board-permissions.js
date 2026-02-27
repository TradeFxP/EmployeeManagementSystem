// board-permissions.js - Management of board permissions for administrators

async function openBoardPermissionModal(teamName) {
    // Ensure bootstrap is available
    const modalEl = document.getElementById('boardPermissionModal');
    if (!modalEl) {
        console.error('boardPermissionModal element not found');
        return;
    }
    const modal = new bootstrap.Modal(modalEl);
    modal.show();

    await loadBoardPermissions(teamName);
}

let rawBoardPermissions = []; // Store raw data for filtering
let currentTeamName = "";
let isDragDropMode = false;

function toggleBoardPermissionMode() {
    isDragDropMode = !isDragDropMode;
    const btn = document.getElementById('toggleDragDropMode');
    if (btn) {
        btn.classList.toggle('btn-outline-info');
        btn.classList.toggle('btn-info');
        btn.innerHTML = isDragDropMode
            ? '<i class="bi bi-person-lines-fill me-1"></i>Normal Mode'
            : '<i class="bi bi-arrows-move me-1"></i>Drag & Drop';
    }

    // Refresh headers and body
    renderBoardPermissionTable(rawBoardPermissions);
}

async function loadBoardPermissions(teamName) {
    const tbody = document.getElementById('permissionTableBody');
    tbody.innerHTML = '<tr><td colspan="11" class="text-center py-5"><div class="spinner-border text-info mb-2"></div><div class="text-muted">Loading team members...</div></td></tr>';

    currentTeamName = teamName;
    const filterSelect = document.getElementById('boardRoleFilter');
    if (filterSelect) filterSelect.value = 'All'; // Reset filter on load

    try {
        const resp = await fetch(`/Tasks/GetBoardPermissions?team=${encodeURIComponent(teamName)}`);
        if (!resp.ok) throw new Error('Failed to fetch permissions');
        rawBoardPermissions = await resp.json();

        renderBoardPermissionTable(rawBoardPermissions);
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="11" class="text-danger text-center py-5">
            <i class="bi bi-exclamation-triangle fs-2 d-block mb-2"></i>
            Failed to load permissions: ${err.message}
        </td></tr>`;
    }
}

function filterBoardPermissions() {
    const filterSelect = document.getElementById('boardRoleFilter');
    if (!filterSelect) return;
    const filterValue = filterSelect.value;

    if (filterValue === 'All') {
        renderBoardPermissionTable(rawBoardPermissions);
    } else {
        // Always show Managers, regardless of the selected filter role
        const filtered = rawBoardPermissions.filter(p => p.role === filterValue || p.role === 'Manager');
        renderBoardPermissionTable(filtered);
    }
}

function renderBoardPermissionTable(data) {
    const table = document.querySelector('#boardPermissionModal table');
    const thead = table.querySelector('thead');
    const tbody = document.getElementById('permissionTableBody');

    if (isDragDropMode) {
        renderDragDropMode(thead, tbody, data);
    } else {
        renderNormalMode(thead, tbody, data);
    }
}

function renderNormalMode(thead, tbody, data) {
    thead.innerHTML = `
        <tr>
            <th class="ps-4 py-3">Member</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Add Col</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Rename</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Reorder</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Del Col</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Edit All</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Delete Task</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Review</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Import Excel</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">Assign</th>
            <th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem;">All</th>
        </tr>
    `;

    renderBoardPermissionRows(data);
}

function renderDragDropMode(thead, tbody, data) {
    const columns = Array.from(document.querySelectorAll('.kanban-column'))
        .filter(c => c.dataset.columnName.toLowerCase() !== 'history')
        .map(c => ({ id: parseInt(c.dataset.columnId), name: c.dataset.columnName }));

    let headersHtml = '<th class="ps-4 py-3">Member</th>';
    columns.forEach(col => {
        headersHtml += `<th class="text-center small text-uppercase fw-bold text-muted" style="font-size: 0.65rem; min-width: 100px;">FROM ${col.name}</th>`;
    });

    thead.innerHTML = `<tr>${headersHtml}</tr>`;
    tbody.innerHTML = '';

    if (data.length === 0) {
        tbody.innerHTML = `<tr><td colspan="${columns.length + 1}" class="text-center py-5 text-muted">No users found.</td></tr>`;
        return;
    }

    data.forEach(p => {
        const tr = document.createElement('tr');
        tr.className = 'border-bottom';
        let cellsHtml = `
            <td class="ps-4 py-3">
                <div class="d-flex align-items-center">
                    <div class="avatar-sm me-3 bg-soft-${getRoleColor(p.role)} text-${getRoleColor(p.role)} rounded-circle d-flex align-items-center justify-content-center fw-bold" style="width: 38px; height: 38px; background: rgba(0,0,0,0.05);">
                        ${p.userName.charAt(0).toUpperCase()}
                    </div>
                    <div>
                        <div class="fw-bold text-dark" style="font-size: 0.85rem;">${p.userName}</div>
                    </div>
                </div>
            </td>
        `;

        columns.forEach(sourceCol => {
            cellsHtml += `<td class="text-center py-2">${renderTransitionCell(p, sourceCol, columns)}</td>`;
        });

        tr.innerHTML = cellsHtml;
        tbody.appendChild(tr);
    });

    // Wire up transition clicks
    tbody.querySelectorAll('.transition-check').forEach(chk => {
        chk.onchange = (e) => updateTransition(e.target);
    });
}

function renderTransitionCell(user, sourceCol, allCols) {
    const allowedTargets = user.allowedTransitions[sourceCol.id] || [];
    const otherCols = allCols.filter(c => c.id !== sourceCol.id);

    if (otherCols.length === 0) return '<span class="text-muted small">N/A</span>';

    // We'll use a small multi-select UI
    let dropdownHtml = `
        <div class="dropdown d-inline-block">
            <button id="btn_allowed_${user.userId}_${sourceCol.id}" class="btn btn-sm btn-light border p-1 px-2" data-bs-toggle="dropdown" data-bs-auto-close="outside" style="font-size: 0.7rem;">
                ${allowedTargets.length} Allowed <i class="bi bi-chevron-down ms-1"></i>
            </button>
            <div class="dropdown-menu p-2 shadow-sm" style="min-width: 180px;">
                <h6 class="dropdown-header px-0 mb-1" style="font-size: 0.7rem;">Allowed Destinations:</h6>
                ${otherCols.map(target => `
                    <div class="form-check small mb-1">
                        <input class="form-check-input transition-check" type="checkbox" 
                               id="tr_${user.userId}_${sourceCol.id}_${target.id}"
                               data-user-id="${user.userId}" 
                               data-source-id="${sourceCol.id}" 
                               data-target-id="${target.id}"
                               ${allowedTargets.includes(target.id) ? 'checked' : ''}>
                        <label class="form-check-label" for="tr_${user.userId}_${sourceCol.id}_${target.id}">
                            ${target.name}
                        </label>
                    </div>
                `).join('')}
            </div>
        </div>
    `;
    return dropdownHtml;
}

async function updateTransition(checkbox) {
    const userId = checkbox.dataset.userId;
    const sourceId = parseInt(checkbox.dataset.sourceId);
    const targetId = parseInt(checkbox.dataset.targetId);
    const isChecked = checkbox.checked;

    // Find the user in raw data to update their local set
    const user = rawBoardPermissions.find(u => u.userId === userId);
    if (!user) return;

    if (!user.allowedTransitions[sourceId]) user.allowedTransitions[sourceId] = [];

    if (isChecked) {
        if (!user.allowedTransitions[sourceId].includes(targetId)) {
            user.allowedTransitions[sourceId].push(targetId);
        }
    } else {
        user.allowedTransitions[sourceId] = user.allowedTransitions[sourceId].filter(id => id !== targetId);
    }

    // UPDATE UI IMMEDIATELY
    const btn = document.getElementById(`btn_allowed_${userId}_${sourceId}`);
    if (btn) {
        const count = user.allowedTransitions[sourceId].length;
        btn.innerHTML = `${count} Allowed <i class="bi bi-chevron-down ms-1"></i>`;
    }

    // Call update API
    checkbox.disabled = true;
    try {
        const resp = await fetch('/Tasks/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(user) // rawBoardPermissions contains the full dto
        });
        if (!resp.ok) throw new Error('Failed to update transitions');

        if (typeof showToast === 'function') showToast('Transitions updated', 'success');
    } catch (err) {
        alert(err.message);
        checkbox.checked = !isChecked; // revert
    } finally {
        checkbox.disabled = false;
    }
}

function renderBoardPermissionRows(data) {
    const tbody = document.getElementById('permissionTableBody');
    tbody.innerHTML = '';

    if (data.length === 0) {
        tbody.innerHTML = '<tr><td colspan="11" class="text-center py-5 text-muted"><i class="bi bi-person-x fs-2 d-block mb-2"></i>No users found for this role.</td></tr>';
        return;
    }

    data.forEach(p => {
        const tr = document.createElement('tr');
        tr.className = 'border-bottom';
        tr.innerHTML = `
            <td class="ps-4 py-3">
                <div class="d-flex align-items-center">
                    <div class="avatar-sm me-3 bg-soft-${getRoleColor(p.role)} text-${getRoleColor(p.role)} rounded-circle d-flex align-items-center justify-content-center fw-bold" style="width: 38px; height: 38px; background: rgba(0,0,0,0.05);">
                        ${p.userName.charAt(0).toUpperCase()}
                    </div>
                    <div>
                        <div class="fw-bold text-dark">${p.userName}</div>
                        <div class="badge bg-light text-muted border small p-1 px-2" style="font-size: 0.7rem;">${p.role}</div>
                    </div>
                </div>
            </td>
            <td class="text-center">${renderSwitch(p.userId, 'canAddColumn', p.canAddColumn)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canRenameColumn', p.canRenameColumn)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canReorderColumns', p.canReorderColumns)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canDeleteColumn', p.canDeleteColumn)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canEditAllFields', p.canEditAllFields)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canDeleteTask', p.canDeleteTask)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canReviewTask', p.canReviewTask)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canImportExcel', p.canImportExcel)}</td>
            <td class="text-center">${renderSwitch(p.userId, 'canAssignTask', p.canAssignTask)}</td>
            <td class="text-center">
                <button class="btn btn-sm btn-link text-info p-0" onclick="grantAllBoardPermissions(this, '${p.userId}', '${currentTeamName}')" title="Grant All Permissions">
                    <i class="bi bi-check-all fs-4"></i>
                </button>
            </td>
        `;
        tbody.appendChild(tr);
    });

    // Wire up change events
    tbody.querySelectorAll('.perm-switch').forEach(chk => {
        chk.onchange = (e) => updatePermission(e.target, currentTeamName);
    });
}


function renderSwitch(userId, field, checked) {
    const id = `sw_${userId}_${field}`;
    return `
        <div class="form-check form-switch d-inline-block">
            <input class="form-check-input perm-switch" type="checkbox" role="switch" id="${id}" 
                   data-user-id="${userId}" data-field="${field}" ${checked ? 'checked' : ''}>
        </div>
    `;
}

function getRoleColor(role) {
    if (!role) return 'secondary';
    role = role.toLowerCase();
    if (role.includes('admin')) return 'danger';
    if (role.includes('manager') && !role.includes('sub')) return 'warning';
    if (role.includes('sub')) return 'primary';
    return 'info';
}

async function updatePermission(checkbox, teamName) {
    const userId = checkbox.getAttribute('data-user-id');
    const val = checkbox.checked;

    // Fetch current row state to send full DTO (safest)
    const row = checkbox.closest('tr');
    const dto = {
        userId: userId,
        teamName: teamName,
        canAddColumn: row.querySelector('[data-field="canAddColumn"]').checked,
        canRenameColumn: row.querySelector('[data-field="canRenameColumn"]').checked,
        canReorderColumns: row.querySelector('[data-field="canReorderColumns"]').checked,
        canDeleteColumn: row.querySelector('[data-field="canDeleteColumn"]').checked,
        canEditAllFields: row.querySelector('[data-field="canEditAllFields"]').checked,
        canDeleteTask: row.querySelector('[data-field="canDeleteTask"]').checked,
        canReviewTask: row.querySelector('[data-field="canReviewTask"]').checked,
        canImportExcel: row.querySelector('[data-field="canImportExcel"]').checked,
        canAssignTask: row.querySelector('[data-field="canAssignTask"]').checked
    };

    checkbox.disabled = true;
    try {
        const resp = await fetch('/Tasks/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        if (typeof showToast === 'function') {
            if (val) {
                showToast('Permission updated successfully', 'success');
            } else {
                showToast('Permission revoked successfully', 'danger');
            }
        }
    } catch (err) {
        if (typeof showToast === 'function') {
            showToast(err.message, 'danger');
        } else {
            alert(err.message);
        }
        checkbox.checked = !val; // revert
    } finally {
        checkbox.disabled = false;
    }
}

async function grantAllBoardPermissions(btn, userId, teamName) {
    const row = btn.closest('tr');
    const switches = row.querySelectorAll('.perm-switch');

    // Determine if we are granting or revoking
    // If ANY are unchecked, we GRANT all. If ALL are checked, we REVOKE all.
    const allChecked = Array.from(switches).every(s => s.checked);
    const targetState = !allChecked;

    if (!targetState) {
        if (!confirm('Revoke all board access for this user?')) return;
    }

    const dto = {
        userId: userId,
        teamName: teamName,
        canAddColumn: targetState,
        canRenameColumn: targetState,
        canReorderColumns: targetState,
        canDeleteColumn: targetState,
        canEditAllFields: targetState,
        canDeleteTask: targetState,
        canReviewTask: targetState,
        canImportExcel: targetState,
        canAssignTask: targetState
    };

    btn.disabled = true;
    try {
        const resp = await fetch('/Tasks/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        switches.forEach(s => s.checked = targetState);

        if (typeof showToast === 'function') {
            if (targetState) {
                showToast('✅ Full board access granted', 'success');
            } else {
                showToast('🛑 Full board access revoked', 'danger');
            }
        }
    } catch (err) {
        if (typeof showToast === 'function') {
            showToast(err.message, 'danger');
        } else {
            alert(err.message);
        }
    } finally {
        btn.disabled = false;
    }
}
