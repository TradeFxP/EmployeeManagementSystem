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

    container.innerHTML = '';

    // Apply grid layout
    container.style.display = 'grid';
    container.style.gridTemplateColumns = 'repeat(2, 1fr)';
    container.style.gap = '16px';
    container.style.alignItems = 'start';

    if (!fields || fields.length === 0) {
        console.log('No custom fields to render');
        return;
    }

    console.log(`Rendering ${fields.length} custom fields`);

    fields.forEach(field => {
        const fieldGroup = document.createElement('div');
        fieldGroup.className = 'mb-1 position-relative field-group';

        // Ensure some fields take full width if needed (optional)
        if (field.fieldType === 'Image' || field.fieldName.toLowerCase().includes('description')) {
            fieldGroup.style.gridColumn = 'span 2';
        }

        // Header container for Label + Actions
        const header = document.createElement('div');
        header.className = 'd-flex justify-content-between align-items-center mb-1';

        const label = document.createElement('label');
        label.className = 'form-label mb-0';
        label.textContent = field.fieldName + (field.isRequired ? ' *' : '');
        label.htmlFor = `field_${field.id}`;
        header.appendChild(label);

        fieldGroup.appendChild(header);

        let input;

        switch (field.fieldType) {
            case 'Number':
                input = document.createElement('input');
                input.type = 'number';
                input.className = 'form-control';
                break;
            case 'Date':
                input = document.createElement('input');
                input.type = 'date';
                input.className = 'form-control';
                break;
            case 'Time':
                input = document.createElement('input');
                input.type = 'time';
                input.className = 'form-control';
                break;
            case 'DateTime':
                input = document.createElement('input');
                input.type = 'datetime-local';
                input.className = 'form-control';
                break;
            case 'Dropdown':
                input = document.createElement('select');
                input.className = 'form-select';

                // Use specified options or default if empty
                const optionsStr = field.dropdownOptions || "";
                const optionsArray = optionsStr ? optionsStr.split(',') : [];

                if (optionsArray.length > 0) {
                    const isTaskType = field.fieldName.toLowerCase().includes('task type');
                    input.innerHTML = optionsArray.map(opt => {
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
                    // Fallback for older fields
                    if (field.fieldName.toLowerCase().includes('priority')) {
                        input.innerHTML = `
                            <option value="Low">Low</option>
                            <option value="Medium">Medium</option>
                            <option value="High">High</option>
                            <option value="Critical">Critical</option>
                        `;
                    } else {
                        input.innerHTML = `<option value="">-- No Options --</option>`;
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
            default: // Text
                input = document.createElement('input');
                input.type = 'text';
                input.className = 'form-control';
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

    // Add "Add Field" button at the bottom (All Roles)
    {
        const addBtnContainer = document.createElement('div');
        addBtnContainer.className = 'mt-3 pt-2 border-top text-center d-flex justify-content-center gap-2';

        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'btn btn-sm btn-outline-primary';
        addBtn.innerHTML = '<i class="bi bi-plus-lg"></i> Add New Field';
        addBtn.onclick = (e) => { e.preventDefault(); addNewCustomField(); };
        addBtnContainer.appendChild(addBtn);

        // Remove duplicate manageBtn from here as it's in the modal footer

        container.appendChild(addBtnContainer);
    }

    // Reset grid for the button container to center it
    const lastChild = container.lastElementChild;
    if (lastChild) {
        lastChild.style.gridColumn = 'span 2';
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

        if (input.value && input.value.trim() !== "") {
            // For select-multiple or just multiple inputs with same ID
            values[fieldId].push(input.value.trim());
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

        if (input.value && input.value.trim() !== "") {
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

// --- Dropdown Options Management ---
function addDropdownOptionRow(value = "") {
    const list = document.getElementById('newFieldOptionsList');
    if (!list) return;

    const row = document.createElement('div');
    row.className = 'input-group input-group-sm mb-2 dropdown-option-row animate__animated animate__fadeInUp';
    row.style.animationDuration = '0.3s';
    row.innerHTML = `
        <input type="text" class="form-control dropdown-option-input border-end-0" value="${value}" placeholder="Option name" />
        <button class="btn btn-outline-danger border-start-0 bg-white" type="button" onclick="this.parentElement.remove()" style="border-color: #dee2e6;">
            <i class="bi bi-trash"></i>
        </button>
    `;
    list.appendChild(row);
}

// Toggle options section based on field type in Create form
$(document).on('change', '#newFieldType', function () {
    const section = document.getElementById('newFieldOptionsSection');
    if (section) {
        if (this.value === 'Dropdown') {
            section.classList.remove('d-none');
            const list = document.getElementById('newFieldOptionsList');
            if (list && list.children.length === 0) {
                addDropdownOptionRow();
            }
        } else {
            section.classList.add('d-none');
        }
    }
});

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


        container.innerHTML = `<div class="row g-3">
            ${fields.map(f => `
                <div class="col-md-6 animate__animated animate__fadeIn" style="animation-duration: 0.4s">
                    <div class="field-item border rounded-3 shadow-sm bg-white h-100 d-flex flex-column" id="field_item_${f.id}" style="transition: transform 0.2s ease, box-shadow 0.2s ease;">
                        <div class="p-3 d-flex justify-content-between align-items-start">
                            <div class="overflow-hidden">
                                <div class="d-flex align-items-center gap-2 mb-1">
                                    <h6 class="mb-0 text-dark fw-bold text-truncate">${f.fieldName}</h6>
                                    ${f.isRequired ? '<span class="badge bg-soft-warning text-warning-emphasis rounded-pill" style="font-size: 10px; background: #fff3cd; border: 1px solid #ffeeba;">Required</span>' : ''}
                                </div>
                                <div class="badge bg-soft-primary text-primary-emphasis rounded-pill" style="font-size: 11px; background: #e7f1ff; border: 1px solid #cfe2ff;">
                                    ${f.fieldType === 'DateTime' ? '📅 Date & Time' :
                f.fieldType === 'Date' ? '📅 Date' :
                    f.fieldType === 'Image' ? '🖼️ Image' :
                        f.fieldType === 'Dropdown' ? '▼ Dropdown' :
                            f.fieldType === 'Number' ? '🔢 Number' : '🔤 Text'}
                                </div>
                            </div>
                            <div class="d-flex gap-1">
                                <button class="btn btn-icon btn-sm btn-light rounded-circle" onclick="toggleFieldEditor(${f.id})" title="Edit Field">
                                    <i class="bi bi-pencil-square"></i>
                                </button>
                                <button class="btn btn-icon btn-sm btn-light rounded-circle text-danger" onclick="deleteField(${f.id})" title="Delete Field">
                                    <i class="bi bi-trash3"></i>
                                </button>
                            </div>
                        </div>

                        ${f.fieldType === 'Dropdown' ? `
                            <div class="px-3 pb-2 flex-grow-1">
                                <div class="d-flex flex-wrap gap-1">
                                    ${(f.dropdownOptions || "").split(',').filter(o => o).slice(0, 4).map(o => `<span class="badge bg-light text-muted border py-1 px-2" style="font-size: 10px;">${o}</span>`).join('')}
                                    ${(f.dropdownOptions || "").split(',').filter(o => o).length > 4 ? `<span class="badge bg-light text-muted border py-1 px-2" style="font-size: 10px;">+${(f.dropdownOptions || "").split(',').filter(o => o).length - 4} more</span>` : ''}
                                    ${!(f.dropdownOptions || "").split(',').filter(o => o).length ? '<span class="text-danger x-small italic">No options</span>' : ''}
                                </div>
                            </div>
                        ` : '<div class="flex-grow-1"></div>'}
                        
                        <!-- Inline Editor (Hidden by default) -->
                        <div id="editor_${f.id}" class="p-3 border-top bg-light d-none animate__animated animate__fadeInDown animate__faster" style="border-radius: 0 0 12px 12px;">
                            <div class="mb-3">
                                <label class="form-label x-small fw-bold text-uppercase text-muted">Field Name</label>
                                <input type="text" id="edit_name_${f.id}" class="form-control form-control-sm shadow-sm" value="${f.fieldName}">
                            </div>
                            <div class="mb-3">
                                <label class="form-label x-small fw-bold text-uppercase text-muted">Field Type</label>
                                <select id="edit_type_${f.id}" class="form-select form-select-sm shadow-sm" onchange="toggleEditOptionsSection(${f.id})">
                                    <option value="Text" ${f.fieldType === 'Text' ? 'selected' : ''}>🔤 Text</option>
                                    <option value="Number" ${f.fieldType === 'Number' ? 'selected' : ''}>🔢 Number</option>
                                    <option value="Date" ${f.fieldType === 'Date' ? 'selected' : ''}>📅 Date</option>
                                    <option value="Time" ${f.fieldType === 'Time' ? 'selected' : ''}>🕐 Time</option>
                                    <option value="DateTime" ${f.fieldType === 'DateTime' ? 'selected' : ''}>🗓️ Date & Time</option>
                                    <option value="Dropdown" ${f.fieldType === 'Dropdown' ? 'selected' : ''}>▼ Dropdown</option>
                                    <option value="Image" ${f.fieldType === 'Image' ? 'selected' : ''}>🖼️ Image (Max 2, 2MB)</option>
                                </select>
                            </div>
                            
                            <div id="edit_options_section_${f.id}" class="${f.fieldType === 'Dropdown' ? '' : 'd-none'} mb-3">
                                <label class="form-label x-small fw-bold text-uppercase text-muted">Dropdown Choices (comma-separated)</label>
                                <textarea id="edit_options_${f.id}" class="form-control form-control-sm shadow-sm" rows="2">${f.dropdownOptions || ''}</textarea>
                            </div>

                            <div class="form-check form-switch mb-3">
                                <input class="form-check-input" type="checkbox" id="edit_required_${f.id}" ${f.isRequired ? 'checked' : ''}>
                                <label class="form-check-label x-small" for="edit_required_${f.id}">Mandatory field</label>
                            </div>

                            <div class="d-grid gap-2 d-md-flex justify-content-md-end">
                                <button class="btn btn-sm btn-white border shadow-sm" onclick="toggleFieldEditor(${f.id})">Cancel</button>
                                <button class="btn btn-sm btn-primary shadow-sm" onclick="saveFieldChanges(${f.id})">Update Field</button>
                            </div>
                        </div>
                    </div>
                </div>
            `).join('')}
        </div>`;

    } catch (error) {
        console.error('Error in loadFieldsList:', error);
        container.innerHTML = '<div class="text-danger">Error loading fields</div>';
    }
}

async function createNewField() {
    const fieldName = document.getElementById('newFieldName').value.trim();
    const fieldType = document.getElementById('newFieldType').value;
    const isRequired = document.getElementById('newFieldRequired').checked;
    const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;

    if (!fieldName) {
        alert('Field name is required');
        return;
    }

    // Collect dropdown options from modal if type is Dropdown
    let dropdownOptions = null;
    if (fieldType === 'Dropdown') {
        const optionInputs = document.querySelectorAll('#newFieldOptionsList .dropdown-option-input');
        const options = Array.from(optionInputs).map(i => i.value.trim()).filter(v => v !== "");
        if (options.length === 0) {
            alert("Please add at least one option for the dropdown.");
            return;
        }
        dropdownOptions = options.join(',');
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
        await loadFieldsList();

        // ✅ Re-render fields in task modals IMMEDIATELY (no page refresh needed)
        await refreshOpenModals();

        if (typeof showToast === 'function') showToast(`✅ Field "${fieldName}" added!`, 'success');

    } catch (error) {
        alert('Error creating field: ' + error.message);
    }
}

async function deleteField(fieldId) {
    if (!confirm('Delete this field? All existing values will be lost.')) return;

    try {
        const response = await fetch('/Tasks/DeleteCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(fieldId)
        });

        if (!response.ok) throw new Error('Failed to delete field');

        // Clear cache
        if (window.customFieldsCache) {
            const team = document.getElementById('kanbanBoard')?.dataset.teamName;
            const cacheKey = team ? `fields_${team}` : 'fields_default';
            delete window.customFieldsCache[cacheKey];
        }

        // Reload list
        const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;
        await loadFieldsList(teamName);

        // ✅ Re-render fields in task modals IMMEDIATELY
        await refreshOpenModals();

        if (typeof showToast === 'function') showToast('🗑️ Field deleted', 'info');

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

function toggleEditOptionsSection(fieldId) {
    const type = document.getElementById(`edit_type_${fieldId}`).value;
    const section = document.getElementById(`edit_options_section_${fieldId}`);
    if (section) {
        if (type === 'Dropdown') {
            section.classList.remove('d-none');
        } else {
            section.classList.add('d-none');
        }
    }
}

async function saveFieldChanges(fieldId) {
    const name = document.getElementById(`edit_name_${fieldId}`).value.trim();
    const type = document.getElementById(`edit_type_${fieldId}`).value;
    const isRequired = document.getElementById(`edit_required_${fieldId}`).checked;
    const options = document.getElementById(`edit_options_${fieldId}`).value.trim();

    if (!name) {
        alert("Field name is required");
        return;
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
                dropdownOptions: options
            })
        });

        if (!response.ok) throw new Error('Failed to update field');

        window.customFieldsCache = null;
        const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;
        await loadFieldsList(teamName);
        await refreshOpenModals();

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
    const teamName = document.getElementById('kanbanBoard')?.dataset.teamName;
    const isPriorityVisible = document.getElementById('showPriorityCheckbox').checked;
    const isDueDateVisible = document.getElementById('showDueDateCheckbox').checked;
    const isTitleVisible = document.getElementById('showTitleCheckbox').checked;
    const isDescriptionVisible = document.getElementById('showDescriptionCheckbox').checked;

    if (!teamName) return;

    try {
        const response = await fetch('/Tasks/UpdateTeamSettings', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                teamName: teamName,
                isPriorityVisible: isPriorityVisible,
                isDueDateVisible: isDueDateVisible,
                isTitleVisible: isTitleVisible,
                isDescriptionVisible: isDescriptionVisible
            })
        });

        if (!response.ok) throw new Error('Failed to update team settings');

        if (typeof showToast === 'function') showToast('✅ Team settings updated!', 'success');

        // Refresh board to show/hide fields
        const currentTeam = document.getElementById('kanbanBoard')?.dataset.teamName;
        if (currentTeam && window.loadTeamBoard) {
            window.loadTeamBoard(currentTeam);
        }

    } catch (error) {
        alert('Error updating settings: ' + error.message);
    }
}
