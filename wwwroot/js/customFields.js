// customFields.js - Custom field management for tasks

window.customFieldsCache = window.customFieldsCache || null;

// ─── helpers ────────────────────────────────────────────────────────────────

/** Bust the cache and re-render fields in whichever modals are currently open. */
async function refreshOpenModals() {
    window.customFieldsCache = null;
    const createEl = document.getElementById('customFieldsContainer');
    const editEl = document.getElementById('editCustomFieldsContainer');
    const team = document.getElementById('kanbanBoard')?.dataset.teamName;
    if (createEl) {
        const savedCreate = collectCustomFieldValues('customFieldsContainer');
        await renderCustomFieldInputs('customFieldsContainer', savedCreate, team);
    }
    if (editEl) {
        const savedEdit = collectCustomFieldValues('editCustomFieldsContainer');
        await renderCustomFieldInputs('editCustomFieldsContainer', savedEdit, team);
    }
}

// Load custom fields from server
async function loadCustomFields(team) {
    const cacheKey = team ? `fields_${team}` : 'fields_default';

    // Return cached if available
    if (window.customFieldsCache && window.customFieldsCache[cacheKey]) {
        console.log('Using cached custom fields for team:', team);
        return window.customFieldsCache[cacheKey];
    }

    window.customFieldsCache = window.customFieldsCache || {};

    try {
        console.log(`Fetching custom fields for team ${team}...`);
        const url = team ? `/Tasks/GetCustomFields?team=${encodeURIComponent(team)}&t=${new Date().getTime()}` : `/Tasks/GetCustomFields?t=${new Date().getTime()}`;
        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load custom fields');

        const fields = await response.json();
        window.customFieldsCache[cacheKey] = fields;
        console.log('Loaded custom fields:', fields);
        return fields;
    } catch (error) {
        console.error('Error loading custom fields:', error);
        return [];
    }
}

// Render custom field inputs in a form - NOW ASYNC
async function renderCustomFieldInputs(containerId, existingValues = {}, team = null) {
    const container = document.getElementById(containerId);
    if (!container) {
        console.error('Container not found:', containerId);
        return;
    }

    // Always load fresh fields
    const fields = await loadCustomFields(team);

    // Sort fields so 'List' (Column) type is always at the bottom
    fields.sort((a, b) => {
        if (a.fieldType === 'List' && b.fieldType !== 'List') return 1;
        if (a.fieldType !== 'List' && b.fieldType === 'List') return -1;
        return a.order - b.order;
    });

    const today = new Date().toISOString().split('T')[0];

    container.innerHTML = '';

    // Apply grid layout
    container.style.display = 'grid';
    container.style.gridTemplateColumns = 'repeat(4, 1fr)';
    container.style.gap = '10px';
    container.style.alignItems = 'start';
    container.style.padding = '4px 0';

    if (!fields || fields.length === 0) {
        console.log('No custom fields to render');
        container.style.display = 'block'; // Fallback
        return;
    }

    console.log(`Rendering ${fields.length} custom fields`);

    fields.forEach(field => {
        const fieldGroup = document.createElement('div');
        fieldGroup.className = 'field-group position-relative p-0';

        // Ensure some fields take full width if needed
        if (field.fieldType === 'Image' || field.fieldName.toLowerCase().includes('description')) {
            fieldGroup.style.gridColumn = 'span 2';
        }

        // Header container for Label + Actions
        const header = document.createElement('div');
        header.className = 'd-flex justify-content-between align-items-center mb-1 px-1';

        const label = document.createElement('label');
        label.className = 'form-label mb-1 fw-bold text-secondary';
        label.style.fontSize = '11px';
        label.style.letterSpacing = '0.3px';
        label.style.textTransform = 'uppercase';
        label.textContent = field.fieldName + (field.isRequired ? ' *' : '');
        label.htmlFor = `field_${field.id}`;
        header.appendChild(label);

        fieldGroup.appendChild(header);

        let input;

        switch (field.fieldType) {
            case 'Number':
                input = document.createElement('input');
                input.type = 'number';
                input.className = 'form-control shadow-sm border-light bg-light rounded-3';
                input.placeholder = '0.00';
                break;
            case 'Date':
                input = document.createElement('input');
                input.type = 'date';
                input.className = 'form-control shadow-sm border-light bg-light rounded-3';
                if (field.fieldName.toLowerCase().includes('follow-up') || field.fieldName.toLowerCase().includes('followup')) {
                    input.min = today;
                }
                break;
            case 'Time':
                input = document.createElement('input');
                input.type = 'time';
                input.className = 'form-control shadow-sm border-light bg-light rounded-3';
                break;
            case 'DateTime':
                input = document.createElement('input');
                input.type = 'datetime-local';
                input.className = 'form-control shadow-sm border-light bg-light rounded-3';
                if (field.fieldName.toLowerCase().includes('follow-up') || field.fieldName.toLowerCase().includes('followup')) {
                    input.min = today + "T00:00";
                }
                break;
            case 'Dropdown':
                {
                    const optionsStr = field.dropdownOptions || "";
                    const optionsArray = optionsStr ? optionsStr.split(',').map(o => o.trim()).filter(o => o) : [];

                    // Logic for Nested Dropdowns
                    const hasNesting = optionsArray.some(o => o.includes(' > '));

                    if (hasNesting) {
                        const nestingContainer = document.createElement('div');
                        nestingContainer.className = 'nested-dropdown-group d-flex flex-row align-items-center gap-2 px-3 py-2 border rounded-3 bg-white shadow-sm';
                        nestingContainer.style.transition = 'all 0.2s ease';

                        // Parse options into hierarchy
                        const hierarchy = {};
                        optionsArray.forEach(o => {
                            if (o.includes(' > ')) {
                                const parts = o.split(' > ');
                                const parent = parts[0].trim();
                                const child = parts[1].trim();
                                if (!hierarchy[parent]) hierarchy[parent] = [];
                                hierarchy[parent].push(child);
                            } else {
                                if (!hierarchy[""]) hierarchy[""] = [];
                                hierarchy[""].push(o);
                            }
                        });

                        const parentSelect = document.createElement('select');
                        parentSelect.className = 'form-select form-select-sm border-0 bg-transparent shadow-none';
                        // Removed flex: 1 to allow content-based width or rely on CSS min-width
                        parentSelect.innerHTML = '<option value="" disabled selected>Select Category</option>';

                        Object.keys(hierarchy).filter(k => k !== "").forEach(p => {
                            parentSelect.innerHTML += `<option value="${p}">${p}</option>`;
                        });

                        const separator = document.createElement('div');
                        separator.className = 'text-muted small px-1 opacity-50';
                        separator.innerHTML = '<i class="bi bi-chevron-right"></i>';

                        const childSelect = document.createElement('select');
                        childSelect.className = 'form-select form-select-sm border-0 bg-transparent shadow-none';
                        childSelect.id = `field_${field.id}`;
                        childSelect.dataset.fieldId = field.id;
                        childSelect.dataset.required = field.isRequired;
                        childSelect.innerHTML = '<option value="" disabled selected>Select Option</option>';
                        childSelect.disabled = true;

                        parentSelect.onchange = () => {
                            const val = parentSelect.value;
                            childSelect.innerHTML = '<option value="" disabled selected>Select Option</option>';
                            if (val && hierarchy[val]) {
                                childSelect.disabled = false;
                                hierarchy[val].forEach(c => {
                                    childSelect.innerHTML += `<option value="${val} > ${c}">${c}</option>`;
                                });
                            } else {
                                childSelect.disabled = true;
                                if (hierarchy[""]) {
                                    hierarchy[""].forEach(c => {
                                        childSelect.innerHTML += `<option value="${c}">${c}</option>`;
                                    });
                                }
                            }
                        };

                        // Hover/Focus effect for the group handled by CSS focus-within

                        // Initial population of child if parent selected
                        const currentVal = (existingValues[field.id] || "").toString();
                        if (currentVal && currentVal.includes(' > ')) {
                            const p = currentVal.split(' > ')[0].trim();
                            parentSelect.value = p;
                            parentSelect.onchange();
                            childSelect.value = currentVal;
                        }

                        nestingContainer.appendChild(parentSelect);
                        nestingContainer.appendChild(separator);
                        nestingContainer.appendChild(childSelect);
                        input = nestingContainer;
                    } else {
                        input = document.createElement('select');
                        input.className = 'form-select';
                        input.id = `field_${field.id}`;

                        if (optionsArray.length > 0) {
                            const isTaskType = field.fieldName.toLowerCase().includes('task type');
                            input.innerHTML = '<option value="">-- Select --</option>' + optionsArray.map(opt => {
                                let prefix = "";
                                if (isTaskType) {
                                    const lowOpt = opt.toLowerCase().trim();
                                    if (lowOpt === 'story') prefix = "📔 ";
                                    else if (lowOpt === 'bug') prefix = "🐞 ";
                                    else if (lowOpt === 'feature') prefix = "⭐ ";
                                    else if (lowOpt === 'enhancement') prefix = "🚀 ";
                                }
                                return `<option value="${opt}">${prefix}${opt}</option>`;
                            }).join('');
                        } else {
                            if (field.fieldName.toLowerCase().includes('priority')) {
                                input.innerHTML = `
                                    <option value="Low">Low</option>
                                    <option value="Medium" selected>Medium</option>
                                    <option value="High">High</option>
                                    <option value="Critical">Critical</option>
                                `;
                            } else {
                                input.innerHTML = `<option value="">-- No Options --</option>`;
                            }
                        }
                    }
                }
                break;
            case 'Image':
                {
                    const maxImages = 2; // User requested limit
                    const imageFields = fields.filter(f => f.fieldType === 'Image');

                    // Count how many image fields across the WHOLE modal have values
                    const filledImages = imageFields.filter(f => {
                        const input = document.getElementById(`field_${f.id}`);
                        return (input && input.value) || existingValues[f.id];
                    }).length;

                    input = document.createElement('div');
                    input.className = 'image-field-container mb-2';

                    // existingValues[field.id] is now an array
                    const fieldValues = Array.isArray(existingValues[field.id]) ? existingValues[field.id] : (existingValues[field.id] ? [existingValues[field.id]] : []);

                    let previewsHtml = '';
                    fieldValues.forEach((val, idx) => {
                        if (val) {
                            const isBase64 = val.startsWith('data:');
                            const displayName = isBase64 ? 'Newly Uploaded Image' : `Image ${idx + 1}`;
                            previewsHtml += `
                                <div class="mb-2 p-1 border rounded bg-light d-flex align-items-center gap-2">
                                    <img src="${val}" class="rounded" style="width: 50px; height: 50px; object-fit: cover; cursor: pointer;" onclick="window.open('${val}', '_blank')" />
                                    <small class="text-muted flex-grow-1 overflow-hidden text-truncate">${displayName}</small>
                                    <button type="button" class="btn btn-xs btn-outline-danger p-0" style="width:24px; height:24px;" onclick="removeFieldImage(${field.id}, '${containerId}', ${idx})">
                                        <i class="bi bi-x"></i>
                                    </button>
                                    <input type="hidden" id="field_${field.id}_${idx}" data-field-id="${field.id}" value="${val}" />
                                </div>
                            `;
                        }
                    });

                    const isLimitReached = filledImages >= maxImages && fieldValues.length === 0; // Only hide if total is reached AND this specific field is empty?
                    // Actually, if total reached, hide all upload inputs
                    const globalLimitReached = filledImages >= maxImages;

                    const countLabel = `<div class="text-end small text-muted mb-1">${filledImages}/${maxImages} images added</div>`;

                    input.innerHTML = `
                        ${countLabel}
                        ${previewsHtml}
                        <div class="input-group input-group-sm" style="${globalLimitReached ? 'display:none' : ''}">
                            <input type="file" class="form-control" accept="image/*" onchange="uploadFieldImage(this, ${field.id}, '${containerId}')" ${globalLimitReached ? 'disabled' : ''} />
                            <span class="input-group-text d-none" id="loader_${field.id}"><span class="spinner-border spinner-border-sm"></span></span>
                        </div>
                        ${globalLimitReached && fieldValues.length === 0 ? '<div class="small text-danger italic">Limit reached (max 2 images)</div>' : ''}
                        
                        <!-- For keeping requirements check working if no images added yet -->
                        ${fieldValues.length === 0 ? `<input type="hidden" id="field_${field.id}" data-field-id="${field.id}" data-required="${field.isRequired}" value="" />` : ''}
                    `;
                }
                break;
                input.dataset.fieldId = field.id;
                input.dataset.required = field.isRequired;
                break;
            case 'List':
                {
                    input = document.createElement('select');
                    input.className = 'form-select shadow-sm border-light bg-light rounded-3';
                    input.id = `field_${field.id}`;
                    input.innerHTML = '<option value="">-- Loading Columns --</option>';

                    // Fetch columns for the team
                    if (team) {
                        fetch(`/Tasks/GetColumns?team=${encodeURIComponent(team)}`)
                            .then(res => res.json())
                            .then(cols => {
                                input.innerHTML = '<option value="">-- Select Column --</option>';
                                cols.forEach(c => {
                                    const isSelected = existingValues[field.id] == c.id ? 'selected' : '';
                                    input.innerHTML += `<option value="${c.id}" ${isSelected}>${c.columnName}</option>`;
                                });
                            })
                            .catch(err => {
                                console.error('Error loading columns:', err);
                                input.innerHTML = '<option value="">Error Loading</option>';
                            });
                    } else {
                        input.innerHTML = '<option value="">Team not specified</option>';
                    }
                }
                break;
            default: // Text
                input = document.createElement('input');
                input.type = 'text';
                input.className = 'form-control shadow-sm border-light bg-light rounded-3';
                break;
        }

        input.dataset.fieldId = field.id;
        input.dataset.required = field.isRequired;

        // For Image fields, we don't want to set id/value on the wrapper div
        if (field.fieldType !== 'Image') {
            input.id = `field_${field.id}`;
            input.value = existingValues[field.id] || '';
            fieldGroup.appendChild(input);
        } else {
            fieldGroup.appendChild(input);
        }

        container.appendChild(fieldGroup);
    });

    // Reset grid for the last row if needed
    const lastChild = container.lastElementChild;
    if (lastChild && fields.length % 4 !== 0) {
        // Optional: styling adjustments for the last field if it doesn't fit the grid perfectly
    }
}

// ================= INLINE CUSTOM FIELD ACTIONS =================

async function addNewCustomField() {
    const name = prompt("Enter field name:");
    if (!name) return;

    try {
        const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;
        const response = await fetch('/Tasks/CreateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: name,
                fieldType: 'Text', // Default to Text for inline simplicity
                isRequired: false,
                teamName: teamName
            })
        });

        if (!response.ok) throw new Error('Failed to create field');

        await refreshOpenModals();

    } catch (error) {
        alert("Error adding field: " + error.message);
    }
}

async function renameCustomField(id, currentName) {
    const newName = prompt("Rename field:", currentName);
    if (!newName || newName === currentName) return;

    try {
        const response = await fetch('/Tasks/UpdateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldId: id,
                fieldName: newName
            })
        });

        if (!response.ok) throw new Error('Failed to rename field');

        await refreshOpenModals();

    } catch (error) {
        alert("Error renaming field: " + error.message);
    }
}

async function deleteCustomFieldInline(id) {
    if (!confirm("Delete this field? All data in this field across all tasks will be lost.")) return;

    try {
        const response = await fetch('/Tasks/DeleteCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(id)
        });

        if (!response.ok) throw new Error('Failed to delete field');

        await refreshOpenModals();

    } catch (error) {
        alert("Error deleting field: " + error.message);
    }
}

// Collect custom field values from form
// Collect custom field values from form
// Collect custom field values from form as Lists to support multiple values per field ID
function collectCustomFieldValues(containerId = 'customFieldsContainer') {
    const values = {};
    const container = document.getElementById(containerId);

    if (!container) {
        console.warn('Custom fields container not found:', containerId);
        return values;
    }

    // Find all elements with data-field-id (inputs, hiddens, etc)
    const inputs = container.querySelectorAll('[data-field-id]');

    inputs.forEach(input => {
        const fieldId = input.dataset.fieldId;
        if (!values[fieldId]) values[fieldId] = [];

        // Safety check for value and trim()
        if (input.value && typeof input.value === 'string') {
            const trimmed = input.value.trim();
            if (trimmed !== "") {
                values[fieldId].push(trimmed);
            }
        }
    });

    console.log(`Collected custom field values from ${containerId}:`, values);
    return values;
}

// Validate required custom fields
function validateCustomFields(containerId = 'customFieldsContainer') {
    const container = document.getElementById(containerId);
    if (!container) return true;

    // We rely on data-required="true" and data-field-id attributes set during render
    // Since images might have multiple hidden inputs for same ID, we group by ID
    const requiredInputs = container.querySelectorAll('[data-required="true"]');

    // Track which field IDs have at least one value
    const fieldValuesMap = {};
    const fieldNamesMap = {};

    requiredInputs.forEach(input => {
        const fieldId = input.dataset.fieldId;
        const fieldName = input.closest('.mb-3')?.querySelector('label')?.innerText.replace(' *', '') || `Field ${fieldId}`;

        if (!fieldValuesMap[fieldId]) {
            fieldValuesMap[fieldId] = [];
            fieldNamesMap[fieldId] = fieldName;
        }

        if (input.value && typeof input.value === 'string' && input.value.trim() !== "") {
            fieldValuesMap[fieldId].push(input.value.trim());
        }
    });

    // Check if any required field has zero values
    for (const fieldId in fieldValuesMap) {
        if (fieldValuesMap[fieldId].length === 0) {
            const fieldName = fieldNamesMap[fieldId];
            if (typeof showToast === 'function') {
                showToast(`${fieldName} is required`, 'warning');
            } else {
                alert(`${fieldName} is required`);
            }

            // Try to focus the first input for this field
            const firstInput = container.querySelector(`[data-field-id="${fieldId}"]`);
            if (firstInput && typeof firstInput.focus === 'function') {
                firstInput.focus();
            }
            return false;
        }
    }

    return true;
}

// ========== ADMIN: MANAGE FIELDS MODAL ==========

function openManageFieldsModal() {
    const modal = new bootstrap.Modal(document.getElementById('manageFieldsModal'));
    const team = document.getElementById('kanbanBoard')?.dataset.teamName;
    loadFieldsList(team);
    modal.show();
}

// --- Grouped Hierarchical Dropdown Management ---
function addParentGroup(parentName = "", containerId = 'newFieldOptionsList') {
    const list = document.getElementById(containerId);
    if (!list) return;

    const groupId = 'group_' + Math.random().toString(36).substr(2, 9);
    const group = document.createElement('div');
    group.className = 'parent-group-container mb-3 p-3 border rounded-3 bg-white shadow-sm animate__animated animate__fadeIn';

    group.innerHTML = `
        <div class="d-flex align-items-center justify-content-between mb-2">
            <div class="d-flex align-items-center gap-2 flex-grow-1">
                <span class="badge bg-primary rounded-pill px-2 py-1" style="font-size: 10px;">PARENT</span>
                <input type="text" class="form-control form-control-sm fw-bold dropdown-parent-name border-0 border-bottom rounded-0 px-1" 
                       placeholder="Category Name (e.g. Status)" value="${parentName}" 
                       style="max-width: 250px; background: transparent; font-size: 13px;">
            </div>
            <button class="btn btn-icon btn-sm btn-light rounded-circle text-danger" onclick="this.closest('.parent-group-container').remove()">
                <i class="bi bi-trash"></i>
            </button>
        </div>
        <div id="${groupId}_children" class="children-list ps-3 border-start ms-2 mb-2" style="border-width: 2px !important; border-color: #e2e8f0 !important;">
            <!-- Children will be added here -->
        </div>
        <button type="button" class="btn btn-xs btn-outline-primary ms-3" onclick="addChildOption('', '${groupId}_children')">
            <i class="bi bi-plus-circle me-1"></i> Add Child Option
        </button>
    `;
    list.appendChild(group);
    return groupId + '_children';
}

function addChildOption(value = "", containerId) {
    const list = document.getElementById(containerId);
    if (!list) return;

    const row = document.createElement('div');
    row.className = 'input-group input-group-sm mb-2 child-option-row animate__animated animate__fadeInLeft animate__faster';

    row.innerHTML = `
        <span class="input-group-text bg-transparent border-0 pe-2 text-muted" style="font-size: 10px;"><i class="bi bi-arrow-return-right"></i></span>
        <input type="text" class="form-control dropdown-child-input rounded-3 shadow-none border-light bg-light" 
               value="${value}" placeholder="Child option name" required style="font-size: 12px;">
        <button class="btn btn-link text-danger p-1 ms-1" type="button" onclick="this.parentElement.remove()" title="Remove Child">
            <i class="bi bi-x-circle"></i>
        </button>
    `;
    list.appendChild(row);
}

// Keep a simple version for standalone options if needed, or just use parent=""
function addStandaloneOption(value = "", containerId = 'newFieldOptionsList') {
    const list = document.getElementById(containerId);
    if (!list) return;

    const row = document.createElement('div');
    row.className = 'input-group input-group-sm mb-2 standalone-option-row animate__animated animate__fadeInUp';

    row.innerHTML = `
        <input type="text" class="form-control dropdown-standalone-input rounded-pill-start border-light bg-light px-3" 
               value="${value}" placeholder="Standalone option" required style="font-size: 12px;">
        <button class="btn btn-outline-danger rounded-pill-end bg-white border-light" type="button" onclick="this.parentElement.remove()">
            <i class="bi bi-trash"></i>
        </button>
    `;
    list.appendChild(row);
}

// New robust population function
function populateFieldOptionsGrouped(field) {
    const containerId = `edit_options_list_${field.id}`;
    const ops = field.dropdownOptions || "";
    if (!ops || typeof addParentGroup !== 'function') return;

    const list = ops.split(',').filter(o => o.trim());
    const groups = {};
    const standalone = [];

    list.forEach(o => {
        if (o.includes(' > ')) {
            const [p, c] = o.split(' > ').map(s => s.trim());
            if (!groups[p]) groups[p] = [];
            groups[p].push(c);
        } else {
            standalone.push(o.trim());
        }
    });

    for (const p in groups) {
        const childId = addParentGroup(p, containerId);
        groups[p].forEach(c => addChildOption(c, childId));
    }
    standalone.forEach(s => addStandaloneOption(s, containerId));
}

// Toggle options section based on field type is now handled by toggleNewOptionsSection() in Index.cshtml

async function loadFieldsList(team) {
    const container = document.getElementById('fieldsList');
    if (!container) return;

    try {
        // Force fresh load for manage fields modal
        if (window.customFieldsCache) {
            const cacheKey = team ? `fields_${team}` : 'fields_default';
            delete window.customFieldsCache[cacheKey];
        }
        const fields = await loadCustomFields(team);

        if (fields.length === 0) {
            container.innerHTML = '<div class="text-muted">No custom fields yet. Add one below.</div>';
            return;
        }


        container.innerHTML = `<div class="row g-1 sortable-fields">
            ${fields.map(f => `
                <div class="col-md-3 field-order-item" data-id="${f.id}">
                    <div class="field-item d-flex flex-column border shadow-sm" id="field_item_${f.id}" 
                         style="border-radius: 6px; background: #fff; transition: all 0.2s ease; min-height: 48px; margin-bottom: 4px; border-color: rgba(0,0,0,0.05) !important;">
                        <div class="p-1 px-2 flex-grow-1 d-flex align-items-center justify-content-between">
                            <div class="d-flex align-items-center gap-2 overflow-hidden">
                                <div class="flex-shrink-0" style="width: 24px; height: 24px; background: #f1f5f9; border-radius: 4px; display: flex; align-items: center; justify-content: center; border: 1px solid #e2e8f0;">
                                    ${f.fieldType === 'DateTime' ? '<i class="bi bi-calendar-event text-primary" style="font-size: 11px;"></i>' :
                f.fieldType === 'Date' ? '<i class="bi bi-calendar text-primary" style="font-size: 11px;"></i>' :
                    f.fieldType === 'Image' ? '<i class="bi bi-image text-primary" style="font-size: 11px;"></i>' :
                        f.fieldType === 'Dropdown' ? '<i class="bi bi-list-nested text-primary" style="font-size: 11px;"></i>' :
                            f.fieldType === 'Number' ? '<i class="bi bi-hash text-primary" style="font-size: 11px;"></i>' :
                                f.fieldType === 'List' ? '<i class="bi bi-columns text-primary" style="font-size: 11px;"></i>' : '<i class="bi bi-fonts text-primary" style="font-size: 11px;"></i>'}
                                </div>
                                <div class="overflow-hidden">
                                    <h6 class="mb-0 text-dark fw-bold text-truncate" style="font-size: 0.75rem; letter-spacing: -0.1px;">${f.fieldName}</h6>
                                    <div class="d-flex align-items-center gap-1" style="font-size: 8px; margin-top: -1px;">
                                        <span class="text-muted text-uppercase fw-semibold opacity-75">${f.fieldType}</span>
                                        ${f.isRequired ? '<span class="text-danger fw-bold">[REQ]</span>' : ''}
                                    </div>
                                </div>
                            </div>
                            
                            <div class="d-flex gap-0 flex-shrink-0">
                                <button class="btn btn-link btn-sm text-muted p-1" style="text-decoration: none;" onclick="toggleFieldEditor(${f.id})" title="Edit">
                                    <i class="bi bi-pencil" style="font-size: 11px;"></i>
                                </button>
                                <button class="btn btn-link btn-sm text-danger p-1" style="text-decoration: none;" onclick="deleteField(${f.id})" title="Deactivate">
                                    <i class="bi bi-x-circle" style="font-size: 11px;"></i>
                                </button>
                            </div>
                        </div>
                        
                        <div id="editor_${f.id}" class="p-2 px-3 border-top bg-light d-none animate__animated animate__fadeIn animate__faster">
                            <div class="row g-2 align-items-center mb-2">
                                <div class="col-6">
                                    <label class="x-small fw-bold text-muted mb-1" style="font-size: 9px; display: block;">TYPE</label>
                                    <select id="edit_type_${f.id}" class="form-select form-select-sm border-secondary-subtle py-0" style="font-size: 0.8rem; height: 28px;" onchange="toggleEditOptionsSection(${f.id})">
                                        <option value="Text" ${f.fieldType === 'Text' ? 'selected' : ''}>Text</option>
                                        <option value="Number" ${f.fieldType === 'Number' ? 'selected' : ''}>Number</option>
                                        <option value="Date" ${f.fieldType === 'Date' ? 'selected' : ''}>Date</option>
                                        <option value="Time" ${f.fieldType === 'Time' ? 'selected' : ''}>Time</option>
                                        <option value="DateTime" ${f.fieldType === 'DateTime' ? 'selected' : ''}>DateTime</option>
                                        <option value="Dropdown" ${f.fieldType === 'Dropdown' ? 'selected' : ''}>Dropdown</option>
                                        <option value="List" ${f.fieldType === 'List' ? 'selected' : ''}>List (Column)</option>
                                        <option value="Image" ${f.fieldType === 'Image' ? 'selected' : ''}>Image</option>
                                    </select>
                                </div>
                                <div class="col-6">
                                    <label class="x-small fw-bold text-muted mb-1" style="font-size: 9px; display: block;">REQUIRED</label>
                                    <div class="form-check form-switch mb-0">
                                        <input class="form-check-input" style="transform: scale(0.7); margin-left: -1.8em;" type="checkbox" id="edit_required_${f.id}" ${f.isRequired ? 'checked' : ''}>
                                    </div>
                                </div>
                            </div>
                            
                            <div class="row g-2 align-items-center mb-2">
                                <div class="col-12">
                                    <label class="x-small fw-bold text-muted mb-1" style="font-size: 9px; display: block;">RENAME FIELD</label>
                                    <input type="text" id="edit_name_${f.id}" class="form-control form-control-sm border-secondary-subtle py-0" style="font-size: 0.8rem; height: 28px;" value="${f.fieldName}">
                                </div>
                            </div>
                            
                            <div id="edit_options_section_${f.id}" class="${f.fieldType === 'Dropdown' ? '' : 'd-none'} mb-2">
                                <label class="x-small fw-bold text-muted mb-1" style="font-size: 9px; display: block;">OPTIONS</label>
                                <div id="edit_options_list_${f.id}" class="edit-options-list mb-1 bg-white rounded border p-1" style="max-height: 120px; overflow-y: auto;">
                                    <!-- Options loaded here -->
                                </div>
                                <div class="d-flex gap-1">
                                    <button type="button" class="btn btn-xs btn-outline-primary py-0" style="font-size: 9px; height: 18px;" onclick="addParentGroup('', 'edit_options_list_${f.id}')">
                                        + Group
                                    </button>
                                    <button type="button" class="btn btn-xs btn-outline-secondary py-0" style="font-size: 9px; height: 18px;" onclick="addStandaloneOption('', 'edit_options_list_${f.id}')">
                                        + Option
                                    </button>
                                </div>
                            </div>

                            <div class="d-flex gap-2 justify-content-end pt-2 border-top mt-1">
                                <button class="btn btn-xs btn-light border py-0 px-2" style="font-size: 10px; height: 22px;" onclick="toggleFieldEditor(${f.id})">Cancel</button>
                                <button class="btn btn-xs btn-primary py-0 px-2" style="font-size: 10px; height: 22px;" onclick="saveFieldChanges(${f.id})">Save Changes</button>
                            </div>
                        </div>
                    </div>
                </div>
            `).join('')}
        </div>`;

        // Populate Dropdown Editors explicitly (script tags in innerHTML don't run)
        fields.forEach(f => {
            if (f.fieldType === 'Dropdown') {
                populateFieldOptionsGrouped(f);
            }
        });

        // Initialize Sortable for fields
        if (typeof Sortable !== 'undefined') {
            const sortableEl = container.querySelector('.sortable-fields');
            if (sortableEl) {
                new Sortable(sortableEl, {
                    animation: 150,
                    handle: '.field-item',
                    onEnd: async function () {
                        const ids = Array.from(sortableEl.querySelectorAll('.field-order-item')).map(el => parseInt(el.dataset.id));
                        try {
                            const response = await fetch('/Tasks/ReorderCustomFields', {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify(ids)
                            });
                            if (response.ok) {
                                if (typeof showToast === 'function') showToast('✅ Field order updated!', 'success');
                                refreshOpenModals();
                            }
                        } catch (e) { console.error('Failed to reorder fields', e); }
                    }
                });
            }
        }

    } catch (error) {
        console.error('Error loading fields list:', error);
        container.innerHTML = '<div class="text-center p-4">Error loading fields.</div>';
    }
}

async function createNewField() {
    const nameEl = document.getElementById('newFieldName');
    const typeEl = document.getElementById('newFieldType');
    const reqEl = document.getElementById('newFieldRequired');

    if (!nameEl) return;
    const fieldName = nameEl.value ? nameEl.value.trim() : '';
    const fieldType = typeEl ? typeEl.value : 'Text';
    const isRequired = reqEl ? reqEl.checked : false;
    const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;

    if (!fieldName) {
        alert('Field name is required');
        return;
    }

    // Collect dropdown options from modal if type is Dropdown
    let dropdownOptions = null;
    if (fieldType === 'Dropdown') {
        const container = document.getElementById('newFieldOptionsList');
        if (container) {
            const collected = [];

            // Collect Groups
            container.querySelectorAll('.parent-group-container').forEach(group => {
                const parentName = group.querySelector('.dropdown-parent-name')?.value.trim();
                if (!parentName) return;

                group.querySelectorAll('.dropdown-child-input').forEach(child => {
                    const childVal = child.value.trim();
                    if (childVal) {
                        collected.push(`${parentName} > ${childVal}`);
                    }
                });
            });

            // Collect Standalone
            container.querySelectorAll('.dropdown-standalone-input').forEach(standalone => {
                const val = standalone.value.trim();
                if (val) {
                    collected.push(val);
                }
            });

            if (collected.length === 0) {
                alert("Please add at least one option for the dropdown.");
                return;
            }
            dropdownOptions = collected.join(',');
        }
    }

    try {
        const response = await fetch('/Tasks/CreateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: fieldName,
                fieldType: fieldType,
                isRequired: isRequired,
                dropdownOptions: dropdownOptions,
                teamName: teamName
            })
        });

        if (!response.ok) throw new Error('Failed to create field');

        // Clear UI in modal
        document.getElementById('newFieldName').value = '';
        document.getElementById('newFieldOptionsList').innerHTML = '';
        document.getElementById('newFieldOptionsSection').classList.add('d-none');
        document.getElementById('newFieldType').value = 'Text';
        document.getElementById('newFieldRequired').checked = false;

        // IMPORTANT: Clear cache so new fields load
        window.customFieldsCache = null;

        // Reload list in modal
        const team = document.getElementById('kanbanBoard')?.dataset.teamName;
        await loadFieldsList(team);

        // ✅ Re-render fields in task modals IMMEDIATELY (no page refresh needed)
        await refreshOpenModals();

        // Refresh board to show potential filter changes or display updates
        if (typeof loadTeamBoard === 'function' && team) loadTeamBoard(team);

        if (typeof showToast === 'function') showToast(`✅ Field "${fieldName}" added!`, 'success');

    } catch (error) {
        alert('Error creating field: ' + error.message);
    }
}

async function deleteField(fieldId) {
    if (!confirm('Deactivate this field? It will be hidden from the UI, but existing task data will be preserved.')) return;

    try {
        const response = await fetch('/Tasks/DeleteCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(fieldId)
        });

        if (!response.ok) throw new Error('Failed to delete field');

        // IMPORTANT: Clear cache so fields re-load
        window.customFieldsCache = null;

        // Reload lists with team context
        const team = document.getElementById('kanbanBoard')?.dataset.teamName;
        await loadFieldsList(team);
        await refreshOpenModals();

        // Refresh board
        if (typeof loadTeamBoard === 'function' && team) loadTeamBoard(team);

        if (typeof showToast === 'function') showToast('🔒 Field deactivated (Data preserved)', 'info');

    } catch (error) {
        alert('Error deleting field: ' + error.message);
    }
}

function toggleFieldEditor(fieldId) {
    const editor = document.getElementById(`editor_${fieldId}`);
    if (editor) {
        editor.classList.toggle('d-none');
    }
}

async function toggleNewOptionsSection() {
    const type = document.getElementById('newFieldType').value;
    const section = document.getElementById('newFieldOptionsSection');
    const list = document.getElementById('newFieldOptionsList');
    const team = document.getElementById('kanbanBoard')?.dataset.teamName;

    if (!section || !list) return;

    if (type === 'Dropdown') {
        section.classList.remove('d-none');
        list.innerHTML = `
            <div class="d-flex gap-2 mb-3 mt-1">
                <button type="button" class="btn btn-xs btn-outline-primary" onclick="addParentGroup('', 'newFieldOptionsList')">
                    <i class="bi bi-collection me-1"></i> Add Parent Group
                </button>
                <button type="button" class="btn btn-xs btn-outline-secondary" onclick="addStandaloneOption('', 'newFieldOptionsList')">
                    <i class="bi bi-plus-circle me-1"></i> Add Standalone Option
                </button>
            </div>
        `;
        // Add one initial group for user convenience
        const childId = addParentGroup('Category', 'newFieldOptionsList');
        addChildOption('Option 1', childId);
    } else if (type === 'List') {
        section.classList.remove('d-none');
        list.innerHTML = '<div class="p-2 text-muted small"><i class="bi bi-info-circle me-1"></i> This field will display board columns as a dropdown.</div>';

        if (team) {
            try {
                const res = await fetch(`/Tasks/GetColumns?team=${encodeURIComponent(team)}`);
                const cols = await res.json();
                if (cols && cols.length > 0) {
                    let preview = '<div class="mt-2 border-top pt-2"><label class="x-small fw-bold text-muted mb-1 text-uppercase" style="font-size: 8px;">Column Preview</label><select class="form-select form-select-sm" disabled>';
                    cols.forEach(c => {
                        preview += `<option>${c.columnName}</option>`;
                    });
                    preview += '</select></div>';
                    list.innerHTML += preview;
                }
            } catch (err) {
                console.error('Error fetching columns for preview:', err);
            }
        }
    } else {
        section.classList.add('d-none');
        list.innerHTML = '';
    }
}

async function toggleEditOptionsSection(fieldId) {
    const type = document.getElementById(`edit_type_${fieldId}`).value;
    const section = document.getElementById(`edit_options_section_${fieldId}`);
    const list = document.getElementById(`edit_options_list_${fieldId}`);
    const team = document.getElementById('kanbanBoard')?.dataset.teamName;

    if (!section || !list) return;

    if (type === 'Dropdown') {
        section.classList.remove('d-none');
        // Logic for dropdown is usually managed by populateFieldOptionsGrouped
    } else if (type === 'List') {
        section.classList.remove('d-none');
        list.innerHTML = '<div class="p-2 text-muted small"><i class="bi bi-info-circle me-1"></i> This field displays board columns.</div>';

        if (team) {
            try {
                const res = await fetch(`/Tasks/GetColumns?team=${encodeURIComponent(team)}`);
                const cols = await res.json();
                if (cols && cols.length > 0) {
                    let preview = '<div class="mt-2 border-top pt-2"><label class="x-small fw-bold text-muted mb-1 text-uppercase" style="font-size: 8px;">Column Preview</label><select class="form-select form-select-sm" disabled>';
                    cols.forEach(c => {
                        preview += `<option>${c.columnName}</option>`;
                    });
                    preview += '</select></div>';
                    list.innerHTML += preview;
                }
            } catch (err) {
                console.error('Error fetching columns for preview:', err);
            }
        }
    } else {
        section.classList.add('d-none');
    }
}

async function saveFieldChanges(fieldId) {
    const nameInput = document.getElementById(`edit_name_${fieldId}`);
    if (!nameInput) return;
    const name = nameInput.value.trim();
    const type = document.getElementById(`edit_type_${fieldId}`).value;
    const isRequired = document.getElementById(`edit_required_${fieldId}`).checked;

    if (!name) {
        alert("Field name is required");
        return;
    }

    let finalOptions = "";
    if (type === 'Dropdown') {
        const container = document.getElementById(`edit_options_list_${fieldId}`);
        if (container) {
            const collected = [];

            // Collect Groups
            container.querySelectorAll('.parent-group-container').forEach(group => {
                const parentName = group.querySelector('.dropdown-parent-name')?.value.trim();
                if (!parentName) return;

                group.querySelectorAll('.dropdown-child-input').forEach(child => {
                    const childVal = child.value.trim();
                    if (childVal) {
                        collected.push(`${parentName} > ${childVal}`);
                    }
                });
            });

            // Collect Standalone
            container.querySelectorAll('.dropdown-standalone-input').forEach(standalone => {
                const val = standalone.value.trim();
                if (val) {
                    collected.push(val);
                }
            });

            finalOptions = collected.join(',');
        }
    }

    try {
        const response = await fetch('/Tasks/UpdateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldId: fieldId,
                fieldName: name,
                fieldType: type,
                isRequired: isRequired,
                dropdownOptions: finalOptions
            })
        });

        if (!response.ok) throw new Error('Failed to update field');

        window.customFieldsCache = null;
        const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;
        await loadFieldsList(teamName);
        await refreshOpenModals();

        // Refresh board to show potential display updates
        if (typeof loadTeamBoard === 'function' && teamName) loadTeamBoard(teamName);

        if (typeof showToast === 'function') showToast("✅ Field updated successfully", "success");

    } catch (error) {
        alert("Error updating field: " + error.message);
    }
}


async function uploadFieldImage(input, fieldId, containerId) {
    if (!input.files || !input.files[0]) return;

    const file = input.files[0];
    const loader = document.getElementById(`loader_${fieldId}`);

    // 2MB Limit (user request)
    if (file.size > 2 * 1024 * 1024) {
        alert("Image size exceeds 2MB limit. Please upload images 2MB or smaller.");
        input.value = "";
        return;
    }

    // Check total image count across the current modal
    // We collect current values first to be accurate
    const currentValues = collectCustomFieldValues(containerId);

    // TEMPORARY: Put the current file's value as something non-empty to check limit correctly
    // or just check against the collected values plus the fact we are adding one.
    const imageFields = Array.from(document.querySelectorAll(`#${containerId} .image-field-container`));
    let filledCount = 0;

    // Count existing values in hidden inputs
    const hiddenInputs = document.querySelectorAll(`#${containerId} input[type="hidden"][id^="field_"]`);
    hiddenInputs.forEach(hi => {
        if (hi.value && hi.value.trim() !== "") filledCount++;
    });

    // If this specific field is empty, adding a file will increase count
    const thisField = document.getElementById(`field_${fieldId}`);
    const willIncreaseCount = !thisField || !thisField.value;

    if (willIncreaseCount && filledCount >= 2) {
        alert("Maximum of 2 images allowed across all fields.");
        input.value = "";
        return;
    }

    if (loader) loader.classList.remove('d-none');

    const formData = new FormData();
    formData.append('file', file);

    try {
        const response = await fetch('/Tasks/UploadCustomFieldImage', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || 'Upload failed');
        }

        const data = await response.json();

        if (data.success) {
            // Collect ALL current values from the form to preserve them during re-render
            const updatedValues = collectCustomFieldValues(containerId);

            // Append the new URL to this field's list
            if (!updatedValues[fieldId]) updatedValues[fieldId] = [];
            updatedValues[fieldId].push(data.url);

            const team = document.getElementById('kanbanBoard')?.dataset.teamName;
            await renderCustomFieldInputs(containerId, updatedValues, team);
        }
    } catch (error) {
        console.error('Upload error:', error);
        alert("Error uploading image: " + error.message);
        input.value = ""; // Clear file input on error
    } finally {
        if (loader) loader.classList.add('d-none');
    }
}

async function removeFieldImage(fieldId, containerId, index) {
    // 1. Collect current values
    const values = collectCustomFieldValues(containerId);

    // 2. Remove the specific value at the given index
    if (values[fieldId] && values[fieldId][index] !== undefined) {
        values[fieldId].splice(index, 1);

        // 3. Re-render the UI with the updated array
        const team = document.getElementById('kanbanBoard')?.dataset.teamName;
        await renderCustomFieldInputs(containerId, values, team);
    }
}

// Load custom fields on page load
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOMContentLoaded - loading custom fields...');
    const team = document.getElementById('kanbanBoard')?.dataset.teamName;
    loadCustomFields(team);
});


async function updateTeamSettings() {
    const board = document.getElementById('kanbanBoard');
    const teamName = board?.dataset.teamName;
    const isPriorityVisible = document.getElementById('showPriorityCheckbox').checked;
    const isDueDateVisible = document.getElementById('showDueDateCheckbox').checked;
    const isTitleVisible = document.getElementById('showTitleCheckbox').checked;
    const isDescriptionVisible = document.getElementById('showDescriptionCheckbox').checked;
    const showOther = document.getElementById('showOtherCheckbox')?.checked !== false;

    if (!teamName) return;

    // 🔥 Optimistic UI Update: Apply visibility IMMEDIATELY to board dataset
    if (board) {
        board.dataset.priorityVisible = showOther && isPriorityVisible ? 'true' : 'false';
        board.dataset.dueDateVisible = showOther && isDueDateVisible ? 'true' : 'false';
        board.dataset.titleVisible = showOther && isTitleVisible ? 'true' : 'false';
        board.dataset.descriptionVisible = showOther && isDescriptionVisible ? 'true' : 'false';
    }

    // ✅ Apply to currently open modals immediately
    applySystemFieldVisibilityToModals();

    try {
        const response = await fetch('/Tasks/UpdateTeamSettings', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                teamName: teamName,
                isPriorityVisible: showOther && isPriorityVisible,
                isDueDateVisible: showOther && isDueDateVisible,
                isTitleVisible: showOther && isTitleVisible,
                isDescriptionVisible: showOther && isDescriptionVisible
            })
        });

        if (!response.ok) throw new Error('Failed to update team settings');

        if (typeof showToast === 'function') showToast('✅ Team settings updated!', 'success');

    } catch (error) {
        console.error('Error updating settings:', error);
    }
}

function applySystemFieldVisibilityToModals() {
    const board = document.getElementById('kanbanBoard');
    if (!board) return;

    const settings = {
        priority: board.dataset.priorityVisible !== 'false',
        dueDate: board.dataset.dueDateVisible !== 'false',
        title: board.dataset.titleVisible !== 'false',
        description: board.dataset.descriptionVisible !== 'false'
    };

    // Helper to hide/show groups
    const toggle = (id, visible) => {
        const el = document.getElementById(id);
        if (el) el.style.display = visible ? 'block' : 'none';
    };

    // Create Modal
    toggle('groupCreateTaskPriority', settings.priority);
    toggle('groupCreateTaskDueDate', settings.dueDate);
    toggle('groupCreateTaskTitle', settings.title);
    toggle('groupCreateTaskDescription', settings.description);

    // Edit Modal
    toggle('groupEditTaskPriority', settings.priority);
    toggle('groupEditTaskDueDate', settings.dueDate);
    toggle('groupEditTaskTitle', settings.title);
    toggle('groupEditTaskDescription', settings.description);
}
