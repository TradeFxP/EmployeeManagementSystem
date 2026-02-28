// board-permissions.js - Management of board permissions for administrators

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

let rawBoardPermissions = []; // Store raw data for filtering
let currentTeamName = "";

async function loadBoardPermissions(teamName) {
    const tbody = document.getElementById('permissionTableBody');
    tbody.innerHTML = '<tr><td colspan="12" class="text-center py-5"><div class="spinner-border text-info mb-2"></div><div class="text-muted">Loading team members...</div></td></tr>';

    try {
        const resp = await fetch(`/Tasks/GetBoardPermissions?team=${encodeURIComponent(teamName)}`);
        if (!resp.ok) throw new Error('Failed to fetch permissions');
        rawBoardPermissions = await resp.json();

        // Populate column filter if it exists
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

function populateColumnFilter(data) {
    const select = document.getElementById('permissionColumnFilter');
    if (!select || data.length === 0) return;

    // Clear existing
    select.innerHTML = '';

    // Get unique columns from first user (all users in team have same columns list in DTO)
    const columns = data[0].columnPermissions || [];
    columns.forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.columnId;
        opt.textContent = c.columnName;
        select.appendChild(opt);
    });
}

function filterBoardPermissions() {
    renderPermissionTable();
}

function renderPermissionTable() {
    const type = document.getElementById('permissionViewType').value;
    const roleFilter = document.getElementById('boardRoleFilter').value;
    const tbody = document.getElementById('permissionTableBody');
    const thead = document.querySelector('#boardPermissionModal table thead tr');

    // 1. Update Headers
    if (type === 'Board') {
        thead.innerHTML = `
            <th class="ps-4 py-3">Member</th>
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
        `;
    } else if (type === 'Column') {
        thead.innerHTML = `
            <th class="ps-4 py-3">Member</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Rename</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Del Col</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Add Task</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Clr Task</th>
            <th class="text-center small text-uppercase fw-bold text-muted">All</th>
        `;
    } else if (type === 'Task') {
        thead.innerHTML = `
            <th class="ps-4 py-3">Member</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Assign</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Create/Edit</th>
            <th class="text-center small text-uppercase fw-bold text-muted">Del Task</th>
            <th class="text-center small text-uppercase fw-bold text-muted">TaskHistory</th>
            <th class="text-center small text-uppercase fw-bold text-muted">All</th>
        `;
    }

    // 2. Filter Data
    let data = rawBoardPermissions;
    if (roleFilter !== 'All') {
        data = data.filter(p => p.role === roleFilter || p.role === 'Manager');
    }

    // 3. Render Rows
    tbody.innerHTML = '';
    if (data.length === 0) {
        tbody.innerHTML = `<tr><td colspan="12" class="text-center py-5 text-muted"><i class="bi bi-person-x fs-2 d-block mb-2"></i>No users found.</td></tr>`;
        return;
    }

    data.forEach(p => {
        const tr = document.createElement('tr');
        tr.className = 'border-bottom';

        let cells = `
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

    // Fetch existing data for the user from our local cache
    const userLocal = rawBoardPermissions.find(u => u.userId === userId);
    if (!userLocal) return;

    // Build the DTO based on current state + new checked value
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
        const resp = await fetch('/Tasks/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        // Update local cache
        Object.assign(userLocal, dto);

        if (typeof showToast === 'function') {
            showToast('Permission updated successfully', 'success');
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
    } else if (viewType === 'Task' || viewType === 'Column') {
        const colId = parseInt(document.getElementById('permissionColumnFilter').value);
        const cp = dto.columnPermissions.find(x => x.columnId === colId);
        if (cp) {
            if (viewType === 'Column') {
                cp.canRename = targetState;
                cp.canDelete = targetState;
                cp.canAddTask = targetState;
            } else {
                cp.canAssignTask = targetState;
                cp.canEditTask = targetState;
                cp.canDeleteTask = targetState;
                cp.canViewHistory = targetState;
            }
        }
    }

    btn.disabled = true;
    try {
        const resp = await fetch('/Tasks/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        // Update local cache and UI
        Object.assign(userLocal, dto);
        switches.forEach(s => s.checked = targetState);

        if (typeof showToast === 'function') {
            showToast('Permissions updated', 'success');
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
