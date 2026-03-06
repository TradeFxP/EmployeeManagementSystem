// board-permissions.js - Management of board permissions for administrators

if (typeof window.rawBoardPermissions === 'undefined') {
    window.rawBoardPermissions = []; // Store raw data for filtering
}
if (typeof window.currentTeamPermissionsName === 'undefined') {
    window.currentTeamPermissionsName = "";
}

async function openBoardPermissionModal(teamName) {
    const modalEl = document.getElementById('boardPermissionModal');
    if (!modalEl) return;
    const modal = new bootstrap.Modal(modalEl);
    modal.show();

    currentTeamName = teamName;

    // Reset filters
    if (document.getElementById('boardRoleFilter')) document.getElementById('boardRoleFilter').value = 'All';
    if (document.getElementById('permissionViewType')) document.getElementById('permissionViewType').value = 'Board';
    if (document.getElementById('columnSelectWrapper')) document.getElementById('columnSelectWrapper').style.setProperty('display', 'none', 'important');

    await loadBoardPermissions(teamName);
}

async function loadBoardPermissions(teamName) {
    const tbody = document.getElementById('permissionTableBody');
    tbody.innerHTML = '<tr><td colspan="12" class="text-center py-1"><div class="spinner-border text-info mb-2"></div><div class="text-muted">Loading team members...</div></td></tr>';

    try {
        const resp = await fetch(`/TaskPermissions/GetBoardPermissions?team=${encodeURIComponent(teamName)}`);
        if (!resp.ok) throw new Error('Failed to fetch permissions');
        rawBoardPermissions = await resp.json();

        // Populate column filter
        populateColumnFilter(rawBoardPermissions);

        renderPermissionTable();
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="12" class="text-danger text-center py-5">
            <i class="bi bi-exclamation-triangle fs-2 d-block mb-2"></i>
            Failed to load permissions: ${err.message}
        </td></tr>`;
    }
}

function onPermissionViewTypeChange() {
    const type = document.getElementById('permissionViewType').value;
    const colWrapper = document.getElementById('columnSelectWrapper');

    if (type === 'Column' || type === 'Task') {
        colWrapper.style.setProperty('display', 'flex', 'important');
    } else {
        colWrapper.style.setProperty('display', 'none', 'important');
    }

    renderPermissionTable();
}

function filterBoardPermissions() {
    renderPermissionTable();
}

function renderPermissionTable() {
    const type = document.getElementById('permissionViewType').value;
    const roleFilter = document.getElementById('boardRoleFilter').value;
    const tbody = document.getElementById('permissionTableBody');
    const table = document.querySelector('#boardPermissionModal table');
    const thead = table.querySelector('thead');

    // Filter Data
    let data = rawBoardPermissions;
    if (roleFilter !== 'All') {
        data = data.filter(p => p.role === roleFilter);
    }

    if (type === 'DragDrop') {
        renderDragDropMode(thead, tbody, data);
        return;
    }

    // Update Headers for Board/Column/Task
    if (type === 'Board') {
        thead.innerHTML = `
            <tr>
                <th class="ps-1 py-1">Member</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Add Col</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Rename</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Reorder</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Del Col</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Edit All</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Del Task</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Review</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Excel</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Assign</th>
                <th class="text-center small text-uppercase fw-bold text-muted">All</th>
            </tr>
        `;
    } else if (type === 'Column') {
        thead.innerHTML = `
            <tr>
                <th class="ps-1 py-1">Member</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Rename</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Del Col</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Add Task</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Clr Task</th>
                <th class="text-center small text-uppercase fw-bold text-muted">All</th>
            </tr>
        `;
    } else if (type === 'Task') {
        thead.innerHTML = `
            <tr>
                <th class="ps-1 py-1">Member</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Assign</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Create/Edit</th>
                <th class="text-center small text-uppercase fw-bold text-muted">Del Task</th>
                <th class="text-center small text-uppercase fw-bold text-muted">TaskHistory</th>
                <th class="text-center small text-uppercase fw-bold text-muted">All</th>
            </tr>
        `;
    }

    renderBoardPermissionRows(data, type);
}

function renderBoardPermissionRows(data, type) {
    const tbody = document.getElementById('permissionTableBody');
    tbody.innerHTML = '';

    if (data.length === 0) {
        tbody.innerHTML = `<tr><td colspan="12" class="text-center py-1 text-muted"><i class="bi bi-person-x fs-2 d-block mb-2"></i>No users found.</td></tr>`;
        return;
    }

    data.forEach(p => {
        const tr = document.createElement('tr');
        tr.className = 'border-bottom';

        let cells = `
            <td class="ps-1 py-1">
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
        `;

        if (type === 'Board') {
            cells += `
                <td class="text-center">${renderSwitch(p.userId, 'canAddColumn', p.canAddColumn)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canRenameColumn', p.canRenameColumn)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canReorderColumns', p.canReorderColumns)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canDeleteColumn', p.canDeleteColumn)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canEditAllFields', p.canEditAllFields)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canDeleteTask', p.canDeleteTask)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canReviewTask', p.canReviewTask)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canImportExcel', p.canImportExcel)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canAssignTask', p.canAssignTask)}</td>
            `;
        } else if (type === 'Column') {
            const colId = parseInt(document.getElementById('permissionColumnFilter').value);
            const cp = p.columnPermissions.find(x => x.columnId === colId) || {};
            cells += `
                <td class="text-center">${renderSwitch(p.userId, 'canRename', cp.canRename, colId)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canDelete', cp.canDelete, colId)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canAddTask', cp.canAddTask, colId)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canClearTasks', cp.canClearTasks, colId)}</td>
            `;
        } else if (type === 'Task') {
            const colId = parseInt(document.getElementById('permissionColumnFilter').value);
            const cp = p.columnPermissions.find(x => x.columnId === colId) || {};
            cells += `
                <td class="text-center">${renderSwitch(p.userId, 'canAssignTask', cp.canAssignTask, colId)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canEditTask', cp.canEditTask, colId)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canDeleteTask', cp.canDeleteTask, colId)}</td>
                <td class="text-center">${renderSwitch(p.userId, 'canViewHistory', cp.canViewHistory, colId)}</td>
            `;
        }

        cells += `
            <td class="text-center">
                <button class="btn btn-sm btn-link text-info p-0" onclick="grantAllBoardPermissions(this, '${p.userId}', '${currentTeamName}')" title="Grant All Permissions">
                    <i class="bi bi-check-all fs-4"></i>
                </button>
            </td>
        `;

        tr.innerHTML = cells;
        tbody.appendChild(tr);
    });

    // Wire up events
    tbody.querySelectorAll('.perm-switch').forEach(chk => {
        chk.onchange = (e) => updatePermission(e.target, currentTeamName);
    });
}

function renderDragDropMode(thead, tbody, data) {
    const columns = Array.from(document.querySelectorAll('.kanban-column'))
        .filter(c => c.dataset.columnName.toLowerCase() !== 'history')
        .map(c => ({ id: parseInt(c.dataset.columnId), name: c.dataset.columnName }));

    let headersHtml = '<th class="ps-1 py-1">Member</th>';
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
            <td class="ps-1 py-1">
                <div class="d-flex align-items-center">
                    <div class="avatar-sm me-3 bg-soft-${getRoleColor(p.role)} text-${getRoleColor(p.role)} rounded-circle d-flex align-items-center justify-content-center fw-bold" style="width: 38px; height: 38px; background: rgba(0,0,0,0.05);">
                        ${p.userName.charAt(0).toUpperCase()}
                    </div>
                    <div>
                        <div class="fw-bold text-dark" style="font-size: 0.85rem;">${p.userName}</div>
                         <div class="badge bg-light text-muted border small p-1 px-2" style="font-size: 0.7rem;">${p.role}</div>
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

    checkbox.disabled = true;
    try {
        const resp = await fetch('/TaskPermissions/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(user)
        });
        if (!resp.ok) throw new Error('Failed to update transitions');

        if (typeof showToast === 'function') showToast('Transitions updated', 'success');
    } catch (err) {
        if (typeof showToast === 'function') showToast(err.message, 'danger');
        checkbox.checked = !isChecked; // revert
    } finally {
        checkbox.disabled = false;
    }
}

function renderSwitch(userId, field, checked, columnId = null) {
    const id = `sw_${userId}_${field}_${columnId || 'board'}`;
    return `
        <div class="form-check form-switch d-inline-block">
            <input class="form-check-input perm-switch" type="checkbox" role="switch" id="${id}" 
                   data-user-id="${userId}" data-field="${field}" 
                   data-column-id="${columnId || ''}" ${checked ? 'checked' : ''}>
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
    const viewType = document.getElementById('permissionViewType').value;

    const userLocal = rawBoardPermissions.find(u => u.userId === userId);
    if (!userLocal) return;

    const dto = JSON.parse(JSON.stringify(userLocal));
    dto.teamName = teamName;

    if (viewType === 'Board') {
        const field = checkbox.getAttribute('data-field');
        dto[field] = val;
    } else if (viewType === 'Column' || viewType === 'Task') {
        const colId = parseInt(checkbox.getAttribute('data-column-id'));
        const field = checkbox.getAttribute('data-field');
        const cp = dto.columnPermissions.find(x => x.columnId === colId);
        if (cp) cp[field] = val;
    }

    checkbox.disabled = true;
    try {
        const resp = await fetch('/TaskPermissions/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        Object.assign(userLocal, dto);

        if (typeof showToast === 'function') {
            showToast('Permission updated successfully', 'success');
        }
    } catch (err) {
        if (typeof showToast === 'function') showToast(err.message, 'danger');
        checkbox.checked = !val; // revert
    } finally {
        checkbox.disabled = false;
    }
}

async function grantAllBoardPermissions(btn, userId, teamName) {
    const row = btn.closest('tr');
    const switches = row.querySelectorAll('.perm-switch');
    const viewType = document.getElementById('permissionViewType').value;

    const allChecked = Array.from(switches).every(s => s.checked);
    const targetState = !allChecked;

    if (!targetState) {
        if (!confirm('Revoke current view access for this user?')) return;
    }

    const userLocal = rawBoardPermissions.find(u => u.userId === userId);
    if (!userLocal) return;

    const dto = JSON.parse(JSON.stringify(userLocal));
    dto.teamName = teamName;

    if (viewType === 'Board') {
        dto.canAddColumn = targetState;
        dto.canRenameColumn = targetState;
        dto.canReorderColumns = targetState;
        dto.canDeleteColumn = targetState;
        dto.canEditAllFields = targetState;
        dto.canDeleteTask = targetState;
        dto.canReviewTask = targetState;
        dto.canImportExcel = targetState;
        dto.canAssignTask = targetState;
    } else if (viewType === 'Column') {
        const colId = parseInt(document.getElementById('permissionColumnFilter').value);
        const cp = dto.columnPermissions.find(x => x.columnId === colId);
        if (cp) {
            cp.canRename = targetState;
            cp.canDelete = targetState;
            cp.canAddTask = targetState;
            cp.canClearTasks = targetState;
        }
    } else if (viewType === 'Task') {
        const colId = parseInt(document.getElementById('permissionColumnFilter').value);
        const cp = dto.columnPermissions.find(x => x.columnId === colId);
        if (cp) {
            cp.canAssignTask = targetState;
            cp.canEditTask = targetState;
            cp.canDeleteTask = targetState;
            cp.canViewHistory = targetState;
        }
    }

    btn.disabled = true;
    try {
        const resp = await fetch('/TaskPermissions/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        Object.assign(userLocal, dto);
        switches.forEach(s => s.checked = targetState);

        if (typeof showToast === 'function') {
            showToast('Permissions updated', 'success');
        }
    } catch (err) {
        if (typeof showToast === 'function') showToast(err.message, 'danger');
    } finally {
        btn.disabled = false;
    }
}

function populateColumnFilter(data) {
    const select = document.getElementById('permissionColumnFilter');
    if (!select || data.length === 0) return;

    select.innerHTML = '';
    const columns = data[0].columnPermissions || [];
    columns.forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.columnId;
        opt.textContent = c.columnName;
        select.appendChild(opt);
    });
}
