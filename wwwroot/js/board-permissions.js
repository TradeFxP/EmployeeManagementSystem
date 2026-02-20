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

async function loadBoardPermissions(teamName) {
    const tbody = document.getElementById('permissionTableBody');
    tbody.innerHTML = '<tr><td colspan="9" class="text-center py-5"><div class="spinner-border text-info mb-2"></div><div class="text-muted">Loading team members...</div></td></tr>';

    try {
        const resp = await fetch(`/Tasks/GetBoardPermissions?team=${encodeURIComponent(teamName)}`);
        if (!resp.ok) throw new Error('Failed to fetch permissions');
        const data = await resp.json();

        tbody.innerHTML = '';
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="9" class="text-center py-5 text-muted"><i class="bi bi-person-x fs-2 d-block mb-2"></i>No users found for this team.</td></tr>';
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
                    <button class="btn btn-sm btn-link text-info p-0" onclick="grantAllBoardPermissions(this, '${p.userId}', '${teamName}')" title="Grant All Permissions">
                        <i class="bi bi-check-all fs-4"></i>
                    </button>
                </td>
            `;
            tbody.appendChild(tr);
        });

        // Wire up change events
        tbody.querySelectorAll('.perm-switch').forEach(chk => {
            chk.onchange = (e) => updatePermission(e.target, teamName);
        });

    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="9" class="text-danger text-center py-5">
            <i class="bi bi-exclamation-triangle fs-2 d-block mb-2"></i>
            Failed to load permissions: ${err.message}
        </td></tr>`;
    }
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
    if (role.includes('manager')) return 'primary';
    if (role.includes('sub')) return 'info';
    return 'success';
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
            showToast('Permission updated successfully', 'success');
        } else {
            console.log('Permission updated successfully');
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
    if (!confirm('Grant all permissions to this user for this board?')) return;

    const row = btn.closest('tr');
    const switches = row.querySelectorAll('.perm-switch');

    const dto = {
        userId: userId,
        teamName: teamName,
        canAddColumn: true,
        canRenameColumn: true,
        canReorderColumns: true,
        canDeleteColumn: true,
        canEditAllFields: true,
        canDeleteTask: true,
        canReviewTask: true,
        canImportExcel: true,
        canAssignTask: true
    };

    btn.disabled = true;
    try {
        const resp = await fetch('/Tasks/UpdateBoardPermission', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });
        if (!resp.ok) throw new Error('Update failed');

        switches.forEach(s => s.checked = true);

        if (typeof showToast === 'function') {
            showToast('✅ Full board access granted', 'success');
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
