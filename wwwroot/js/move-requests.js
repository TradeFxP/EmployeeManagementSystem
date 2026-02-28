/* move-requests.js */

/**
 * Opens the modal for a user to request a task move.
 */
function openMoveRequestModal(taskId, taskTitle, currentColumnId, currentColumnName) {
    document.getElementById('moveReqTaskId').value = taskId;
    document.getElementById('moveReqTaskTitle').textContent = taskTitle;
    document.getElementById('moveReqFromColName').textContent = currentColumnName;

    const toColSelect = document.getElementById('moveReqToColId');
    toColSelect.innerHTML = '';

    document.querySelectorAll('.kanban-column:not(.col-history)').forEach(col => {
        const colId = parseInt(col.getAttribute('data-column-id'));
        const colName = col.getAttribute('data-column-name');

        if (colId && colId !== currentColumnId) {
            const opt = document.createElement('option');
            opt.value = colId;
            opt.textContent = colName;
            toColSelect.appendChild(opt);
        }
    });

    const modal = new bootstrap.Modal(document.getElementById('submitMoveRequestModal'));
    modal.show();
}

/**
 * Submits the move request to the server.
 */
function confirmSubmitMoveRequest() {
    const taskId = parseInt(document.getElementById('moveReqTaskId').value);
    const toColumnId = parseInt(document.getElementById('moveReqToColId').value);

    if (!toColumnId) {
        showToast('Please select a target column.', 'warning');
        return;
    }

    fetch('/Tasks/SubmitMoveRequest', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ taskId, toColumnId })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showToast('✅ ' + data.message, 'success');
                const m = bootstrap.Modal.getInstance(document.getElementById('submitMoveRequestModal'));
                if (m) m.hide();
            } else {
                showToast('❌ ' + (data.message || 'Failed to submit request'), 'danger');
            }
        })
        .catch(err => {
            console.error(err);
            showToast('❌ Failed to connect to server', 'danger');
        });
}

/**
 * Opens the management modal for admins/managers to view all requests.
 */
function openMoveRequestsModal(teamName) {
    window.currentTeamName = teamName;
    const tbody = document.getElementById('moveRequestsTableBody');
    tbody.innerHTML = '<tr><td colspan="4" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary"></div></td></tr>';

    fetch(`/Tasks/GetBoardMoveRequests?teamName=${encodeURIComponent(teamName)}`)
        .then(res => {
            if (!res.ok) throw new Error('Unauthorized or server error');
            return res.json();
        })
        .then(requests => {
            renderMoveRequests(requests);
            const modal = new bootstrap.Modal(document.getElementById('moveRequestsModal'));
            modal.show();
        })
        .catch(err => {
            console.error(err);
            tbody.innerHTML = `<tr><td colspan="4" class="text-center text-danger py-4">${err.message}</td></tr>`;
        });
}

/**
 * Renders the move requests table.
 */
function renderMoveRequests(requests) {
    const tbody = document.getElementById('moveRequestsTableBody');
    const emptyState = document.getElementById('moveRequestsEmptyState');

    if (!requests || requests.length === 0) {
        tbody.innerHTML = '';
        emptyState.classList.remove('d-none');
        return;
    }

    emptyState.classList.add('d-none');
    tbody.innerHTML = requests.map(r => {
        let statusBadge = '';
        if (r.status === 'Pending') statusBadge = '<span class="badge bg-warning text-dark px-2 py-1"><i class="bi bi-hourglass-split me-1"></i>Pending</span>';
        else if (r.status === 'Approved') statusBadge = '<span class="badge bg-success px-2 py-1"><i class="bi bi-check-circle me-1"></i>Approved</span>';
        else statusBadge = '<span class="badge bg-danger px-2 py-1"><i class="bi bi-x-circle me-1"></i>Rejected</span>';

        // Highlight new requests
        const rowClass = r.isNew && r.status === 'Pending' ? 'table-primary fw-bold border-start border-4 border-primary' : '';
        const timeStr = r.requestedAtFormatted || new Date(r.requestedAt).toLocaleString();

        return `
            <tr class="${rowClass}">
                <td class="ps-3">
                    <div class="fw-bold text-dark">${escapeHtml(r.taskTitle)}</div>
                    <div class="text-muted" style="font-size: 0.75rem;">By <span class="text-primary fw-bold">${escapeHtml(r.requestedByUserName)}</span></div>
                    <div class="text-muted smaller" style="font-size: 0.65rem;"><i class="bi bi-clock me-1"></i>${timeStr}</div>
                </td>
                <td>
                    <div class="d-flex align-items-center gap-2">
                        <span class="badge bg-light text-secondary border small">${escapeHtml(r.fromColumnName)}</span>
                        <i class="bi bi-arrow-right text-primary"></i>
                        <span class="badge bg-primary text-white small">${escapeHtml(r.toColumnName)}</span>
                    </div>
                </td>
                <td>
                    ${statusBadge}
                    ${r.status !== 'Pending' ? `
                        <div class="text-muted mt-1" style="font-size: 0.7rem;">
                            <i class="bi bi-person-check me-1"></i>${escapeHtml(r.handledByUserName || 'Admin')}
                        </div>
                    ` : ''}
                </td>
                <td class="text-end pe-3">
                    ${r.status === 'Pending' ? `
                        <div class="btn-group shadow-sm">
                            <button class="btn btn-sm btn-success" onclick="handleMoveRequest('${r.id}', true)" title="Approve">
                                <i class="bi bi-check-lg"></i>
                            </button>
                            <button class="btn btn-sm btn-danger" onclick="handleMoveRequest('${r.id}', false)" title="Reject">
                                <i class="bi bi-x-lg"></i>
                            </button>
                        </div>
                    ` : `
                        <div class="dropdown">
                            <button class="btn btn-sm btn-link text-muted" type="button" data-bs-toggle="dropdown">
                                <i class="bi bi-info-circle"></i>
                            </button>
                            <div class="dropdown-menu dropdown-menu-end p-3 shadow border-0" style="min-width: 250px;">
                                <h6 class="dropdown-header px-0 mb-2 border-bottom">Reply / Note</h6>
                                <p class="mb-0 small text-dark">${escapeHtml(r.adminReply || 'No extra note provided.')}</p>
                                <div class="mt-2 text-muted smaller">${r.handledAtFormatted || ''}</div>
                            </div>
                        </div>
                    `}
                </td>
            </tr>
        `;
    }).join('');
}

/**
 * Handle request modal
 */
function handleMoveRequest(requestId, approved) {
    document.getElementById('handleRequestId').value = requestId;
    document.getElementById('handleRequestApproved').value = approved;
    document.getElementById('handleRequestReply').value = '';

    const title = document.getElementById('handleRequestTitle');
    const header = document.getElementById('handleRequestHeader');
    const submitBtn = document.getElementById('handleRequestSubmitBtn');

    if (approved) {
        title.innerHTML = '<i class="bi bi-check-circle-fill me-2"></i>Approve Move Request';
        header.className = 'modal-header bg-success text-white';
        submitBtn.className = 'btn btn-success px-4 shadow-sm';
        submitBtn.textContent = 'Approve & Move';
    } else {
        title.innerHTML = '<i class="bi bi-x-circle-fill me-2"></i>Reject Move Request';
        header.className = 'modal-header bg-danger text-white';
        submitBtn.className = 'btn btn-danger px-4 shadow-sm';
        submitBtn.textContent = 'Reject Request';
    }

    const modal = new bootstrap.Modal(document.getElementById('handleMoveRequestModal'));
    modal.show();
}

/**
 * Confirm handling
 */
function confirmHandleMoveRequest() {
    const requestId = document.getElementById('handleRequestId').value;
    const approved = document.getElementById('handleRequestApproved').value === 'true';
    const adminReply = document.getElementById('handleRequestReply').value;

    fetch('/Tasks/HandleMoveRequest', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestId, approved, adminReply })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showToast(approved ? '✅ Request approved and task moved!' : '❌ Request rejected.', 'success');
                const m = bootstrap.Modal.getInstance(document.getElementById('handleMoveRequestModal'));
                if (m) m.hide();

                // Refresh table
                if (window.currentTeamName) {
                    fetch(`/Tasks/GetBoardMoveRequests?teamName=${encodeURIComponent(window.currentTeamName)}`)
                        .then(res => res.json())
                        .then(requests => renderMoveRequests(requests));
                }
            } else {
                showToast('❌ Failed to handle request', 'danger');
            }
        })
        .catch(err => {
            console.error(err);
            showToast('❌ Server error connection failed', 'danger');
        });
}
