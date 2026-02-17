// column_filter.js
// Handles per-column search and sort (Newest/Oldest)

// State to track sort order per column (default: desc/newest)
const columnSortState = {};

function toggleColumnFilter(columnId) {
    const toolbar = document.getElementById(`column-toolbar-${columnId}`);
    if (!toolbar) return;

    if (toolbar.style.display === 'none') {
        toolbar.style.display = 'block';
        // Focus search input
        setTimeout(() => document.getElementById(`search-input-${columnId}`)?.focus(), 100);
    } else {
        toolbar.style.display = 'none';
    }
}

function setColumnSort(columnId, direction) {
    columnSortState[columnId] = direction;

    // Update UI buttons
    const btnDesc = document.getElementById(`sort-desc-${columnId}`);
    const btnAsc = document.getElementById(`sort-asc-${columnId}`);

    if (btnDesc && btnAsc) {
        if (direction === 'desc') {
            btnDesc.classList.add('active', 'bg-secondary', 'text-white');
            btnAsc.classList.remove('active', 'bg-secondary', 'text-white');
        } else {
            btnDesc.classList.remove('active', 'bg-secondary', 'text-white');
            btnAsc.classList.add('active', 'bg-secondary', 'text-white');
        }
    }

    applyColumnFilter(columnId);
}

function applyColumnFilter(columnId) {
    const searchInput = document.getElementById(`search-input-${columnId}`);
    const searchTerm = searchInput ? searchInput.value.toLowerCase().trim() : '';
    const sortDir = columnSortState[columnId] || 'desc'; // Default Newest

    const container = document.querySelector(`.kanban-tasks[data-column-id='${columnId}']`);
    if (!container) return;

    // 1. Get all task cards
    const cards = Array.from(container.querySelectorAll('.task-card'));

    // 2. Filter & Sort
    const visibleCards = cards.filter(card => {
        const title = card.querySelector('.task-title-text')?.textContent.toLowerCase() || '';
        const desc = card.querySelector('.task-desc-text')?.textContent.toLowerCase() || '';

        // Simple text match
        const isMatch = title.includes(searchTerm) || desc.includes(searchTerm);

        // Show/Hide based on match
        card.style.display = isMatch ? 'block' : 'none';

        return isMatch;
    });

    // 3. Sort logic
    visibleCards.sort((a, b) => {
        const dateA = new Date(a.dataset.createdAt || 0);
        const dateB = new Date(b.dataset.createdAt || 0);

        if (sortDir === 'asc') {
            return dateA - dateB; // Oldest first
        } else {
            return dateB - dateA; // Newest first
        }
    });

    // 4. Re-append in new order (only visible ones need ordering, but we must append all to maintain DOM)
    // Actually, we should re-append ALL cards, but sorted. 
    // Hidden cards order doesn't matter much, but let's keep them sorted too for consistency if filter is cleared.

    // Let's sort ALL cards based on current criteria, regardless of visibility
    cards.sort((a, b) => {
        const dateA = new Date(a.dataset.createdAt || 0);
        const dateB = new Date(b.dataset.createdAt || 0);

        if (sortDir === 'asc') {
            return dateA - dateB;
        } else {
            return dateB - dateA;
        }
    });

    // Re-inject into DOM
    cards.forEach(card => container.appendChild(card));
}
