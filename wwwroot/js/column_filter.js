// column_filter.js
// Handles per-column search and sort (Time with Asc/Desc)

// State: { columnId: { type: 'time', dir: 'asc'|'desc' } }
const columnSortState = {};
let clickTimer = null;

function toggleColumnFilter(columnId) {
    const toolbar = document.getElementById(`column-toolbar-${columnId}`);
    if (!toolbar) return;

    if (toolbar.style.display === 'none') {
        toolbar.style.display = 'block';
        setTimeout(() => document.getElementById(`search-input-${columnId}`)?.focus(), 100);
    } else {
        toolbar.style.display = 'none';
    }
}

/**
 * Single Click: Newest First (Desc) - ▲
 */
function handleColumnSortClick(columnId, sortType) {
    if (columnId === 'history') return; // History has its own sorting if needed, but we follow task pattern

    // Clear any existing double click timer
    if (columnId in clickTimers) {
        clearTimeout(clickTimers[columnId]);
    }

    // Set a timer for single click
    clickTimers[columnId] = setTimeout(() => {
        delete clickTimers[columnId];
        // SINGLE CLICK -> Newest First (desc) -> ▲
        setColumnSortState(columnId, 'time', 'desc');
    }, 250);
}

function handleColumnSortDblClick(columnId, sortType) {
    if (columnId === 'history') return;

    // Clear the single click timer
    if (columnId in clickTimers) {
        clearTimeout(clickTimers[columnId]);
        delete clickTimers[columnId];
    }

    // DOUBLE CLICK -> Oldest First (asc) -> ▼
    setColumnSortState(columnId, 'time', 'asc');
}

function setColumnSortState(columnId, type, dir) {
    columnSortState[columnId] = { type, dir };

    // Update UI - Only Clock Icon
    const timeBtn = document.getElementById(`sort-time-${columnId}`);
    if (timeBtn) {
        const indicator = timeBtn.querySelector('.sort-indicator');
        if (indicator) {
            indicator.textContent = dir === 'desc' ? '▲' : '▼';
        }
        timeBtn.classList.add('active', 'btn-primary');
        timeBtn.classList.remove('btn-outline-secondary');
    }

    applyColumnFilter(columnId);
}

function applyColumnFilter(columnId) {
    const searchInput = document.getElementById(`search-input-${columnId}`);
    const searchTerm = searchInput ? searchInput.value.toLowerCase().trim() : '';

    // Default: Time Desc (Newest)
    const state = columnSortState[columnId] || { type: 'time', dir: 'desc' };

    const container = document.querySelector(`.kanban-tasks[data-column-id='${columnId}']`);
    if (!container) return;

    const cards = Array.from(container.querySelectorAll('.task-card'));

    // 1. Filter
    cards.forEach(card => {
        const title = card.querySelector('.task-title-text')?.textContent.toLowerCase() || '';
        const desc = card.querySelector('.task-desc-text')?.textContent.toLowerCase() || '';
        const isMatch = title.includes(searchTerm) || desc.includes(searchTerm);

        if (isMatch) {
            card.style.display = 'block';
            card.classList.remove('d-none');
        } else {
            card.style.display = 'none';
            card.classList.add('d-none');
        }
    });

    // 2. Sort
    cards.sort((a, b) => {
        let valA, valB;
        valA = new Date(a.dataset.createdAt || 0);
        valB = new Date(b.dataset.createdAt || 0);

        if (state.dir === 'asc') {
            return valA - valB; // Oldest first
        } else {
            return valB - valA; // Newest first
        }
    });

    // 3. Re-append
    cards.forEach(card => container.appendChild(card));

    if (typeof updateColumnCounts === 'function') {
        updateColumnCounts();
    }
}
