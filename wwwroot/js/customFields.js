// customFields.js - Custom field management for tasks

window.customFieldsCache = window.customFieldsCache || null;

// Load custom fields from server
async function loadCustomFields() {
    // Return cached if available
    if (window.customFieldsCache !== null) {
        console.log('Using cached custom fields:', window.customFieldsCache);
        return window.customFieldsCache;
    }

    try {
        console.log('Fetching custom fields from server...');
        const response = await fetch(`/Tasks/GetCustomFields?t=${new Date().getTime()}`);
        if (!response.ok) throw new Error('Failed to load custom fields');

        window.customFieldsCache = await response.json();
        console.log('Loaded custom fields:', window.customFieldsCache);
        return window.customFieldsCache;
    } catch (error) {
        console.error('Error loading custom fields:', error);
        return [];
    }
}

// Render custom field inputs in a form - NOW ASYNC
async function renderCustomFieldInputs(containerId, existingValues = {}) {
    const container = document.getElementById(containerId);
    if (!container) {
        console.error('Container not found:', containerId);
        return;
    }

    // Always load fresh fields
    const fields = await loadCustomFields();

    container.innerHTML = '';

    if (!fields || fields.length === 0) {
        console.log('No custom fields to render');
        return;
    }

    console.log(`Rendering ${fields.length} custom fields`);

    fields.forEach(field => {
        const fieldGroup = document.createElement('div');
        fieldGroup.className = 'mb-3 position-relative field-group'; // Added position-relative

        // Header container for Label + Actions
        const header = document.createElement('div');
        header.className = 'd-flex justify-content-between align-items-center mb-1';

        const label = document.createElement('label');
        label.className = 'form-label mb-0';
        label.textContent = field.fieldName + (field.isRequired ? ' *' : '');
        label.htmlFor = `field_${field.id}`;
        header.appendChild(label);

        // Inline Actions (All Roles for local customization/UI preference)
        if (true) {
            const actions = document.createElement('div');
            actions.className = 'btn-group btn-group-sm';

            // Rename Button
            const renameBtn = document.createElement('button');
            renameBtn.type = 'button';
            renameBtn.className = 'btn btn-link text-secondary p-0 me-2';
            renameBtn.innerHTML = '<i class="bi bi-pencil"></i>';
            renameBtn.title = 'Rename Field';
            renameBtn.onclick = (e) => { e.preventDefault(); renameCustomField(field.id, field.fieldName); };
            actions.appendChild(renameBtn);

            // Delete Button
            const deleteBtn = document.createElement('button');
            deleteBtn.type = 'button';
            deleteBtn.className = 'btn btn-link text-danger p-0';
            deleteBtn.innerHTML = '<i class="bi bi-trash"></i>';
            deleteBtn.title = 'Delete Field';
            deleteBtn.onclick = (e) => { e.preventDefault(); deleteCustomFieldInline(field.id); };
            actions.appendChild(deleteBtn);

            header.appendChild(actions);
        }
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
            case 'DateTime':
                input = document.createElement('input');
                input.type = 'datetime-local';
                input.className = 'form-control';
                break;
            case 'Dropdown':
                input = document.createElement('select');
                input.className = 'form-select';
                // TODO: Add dropdown options from field configuration
                break;
            default: // Text
                input = document.createElement('input');
                input.type = 'text';
                input.className = 'form-control';
                break;
        }

        input.id = `field_${field.id}`;
        input.dataset.fieldId = field.id;
        input.dataset.required = field.isRequired;
        input.value = existingValues[field.id] || '';

        if (field.isRequired) {
            input.required = true;
        }

        fieldGroup.appendChild(input);
        container.appendChild(fieldGroup);
    });

    // Add "Add Field" button at the bottom (All Roles)
    {
        const addBtnContainer = document.createElement('div');
        addBtnContainer.className = 'mt-3 pt-2 border-top text-center';

        const addBtn = document.createElement('button');
        addBtn.type = 'button';
        addBtn.className = 'btn btn-sm btn-outline-primary';
        addBtn.innerHTML = '<i class="bi bi-plus-lg"></i> Add New Field';
        addBtn.onclick = (e) => { e.preventDefault(); addNewCustomField(); };
        addBtnContainer.appendChild(addBtn);

        container.appendChild(addBtnContainer);
    }
}

// ================= INLINE CUSTOM FIELD ACTIONS =================

async function addNewCustomField() {
    const name = prompt("Enter field name:");
    if (!name) return;

    try {
        const response = await fetch('/Tasks/CreateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: name,
                fieldType: 'Text', // Default to Text for inline simplicity
                isRequired: false
            })
        });

        if (!response.ok) throw new Error('Failed to create field');

        // Clear cache and reload
        window.customFieldsCache = null;
        await renderCustomFieldInputs('customFieldsContainer', collectCustomFieldValues()); // Preserve values

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

        // Clear cache and reload
        window.customFieldsCache = null;
        await renderCustomFieldInputs('customFieldsContainer', collectCustomFieldValues());

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

        // Clear cache and reload
        window.customFieldsCache = null;
        await renderCustomFieldInputs('customFieldsContainer', collectCustomFieldValues());

    } catch (error) {
        alert("Error deleting field: " + error.message);
    }
}

// Collect custom field values from form
// Collect custom field values from form
function collectCustomFieldValues(containerId = 'customFieldsContainer') {
    const values = {};
    const container = document.getElementById(containerId);

    if (!container) {
        console.warn('Custom fields container not found:', containerId);
        return values;
    }

    // Find all inputs with data-field-id
    const inputs = container.querySelectorAll('[data-field-id]');

    inputs.forEach(input => {
        if (input.value && input.value.trim() !== "") {
            values[input.dataset.fieldId] = input.value.trim();
        }
    });

    console.log(`Collected custom field values from ${containerId}:`, values);
    return values;
}

// Validate required custom fields
function validateCustomFields(containerId = 'customFieldsContainer') {
    if (!window.customFieldsCache) return true;

    // We can't easily rely on just cache because we need to check if the field exists in the SPECIFIC container
    // But for now, let's just check the inputs inside the container

    const container = document.getElementById(containerId);
    if (!container) return true;

    for (const field of window.customFieldsCache) {
        if (field.isRequired) {
            // Find input within the container
            const input = container.querySelector(`[data-field-id="${field.id}"]`);

            // If input exists in this form and is required
            if (input && !input.value.trim()) {
                alert(`${field.fieldName} is required`);
                input.focus();
                return false;
            }
        }
    }

    return true;
}

// ========== ADMIN: MANAGE FIELDS MODAL ==========

function openManageFieldsModal() {
    const modal = new bootstrap.Modal(document.getElementById('manageFieldsModal'));
    loadFieldsList();
    modal.show();
}

async function loadFieldsList() {
    const container = document.getElementById('fieldsList');
    if (!container) return;

    try {
        // Force fresh load for manage fields modal
        window.customFieldsCache = null;
        const fields = await loadCustomFields();

        if (fields.length === 0) {
            container.innerHTML = '<div class="text-muted">No custom fields yet. Add one below.</div>';
            return;
        }


        container.innerHTML = fields.map(f => `
            <div class="field-item d-flex justify-content-between align-items-center p-2 mb-2 border rounded" data-field-id="${f.id}">
                <div class="flex-grow-1">
                    <strong>${f.fieldName}</strong>
                    <span class="badge bg-secondary ms-2">${f.fieldType === 'DateTime' ? 'Date & Time' : f.fieldType}</span>
                    ${f.isRequired ? '<span class="badge bg-warning text-dark ms-1">Required</span>' : ''}
                </div>
                <div class="d-flex gap-1">
                    <button class="btn btn-sm btn-outline-primary" onclick="changeFieldType(${f.id}, '${f.fieldType}'); event.stopPropagation();" title="Change Type">
                        <i class="bi bi-arrow-repeat"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="deleteField(${f.id}); event.stopPropagation();">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            </div>
        `).join('');

    } catch (error) {
        console.error('Error in loadFieldsList:', error);
        container.innerHTML = '<div class="text-danger">Error loading fields</div>';
    }
}

async function createNewField() {
    const fieldName = document.getElementById('newFieldName').value.trim();
    const fieldType = document.getElementById('newFieldType').value;
    const isRequired = document.getElementById('newFieldRequired').checked;

    if (!fieldName) {
        alert('Field name is required');
        return;
    }

    try {
        const response = await fetch('/Tasks/CreateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: fieldName,
                fieldType: fieldType,
                isRequired: isRequired
            })
        });

        if (!response.ok) throw new Error('Failed to create field');

        // Clear form
        document.getElementById('newFieldName').value = '';
        document.getElementById('newFieldRequired').checked = false;

        // IMPORTANT: Clear cache so new fields load
        window.customFieldsCache = null;

        // Reload list in modal
        await loadFieldsList();

        // Re-render fields in create task modal if it's open
        await renderCustomFieldInputs('customFieldsContainer');

        console.log('Field created successfully and forms updated');

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
        window.customFieldsCache = null;

        // Reload list
        await loadFieldsList();

        // Re-render fields in create task modal if it's open
        await renderCustomFieldInputs('customFieldsContainer');

        console.log('Field deleted successfully');

    } catch (error) {
        alert('Error deleting field: ' + error.message);
    }
}

async function changeFieldType(fieldId, currentType) {
    const types = ['Text', 'Number', 'Date', 'DateTime'];
    const typeLabels = { 'Text': 'Text', 'Number': 'Number', 'Date': 'Date', 'DateTime': 'Date & Time' };

    const options = types.map((t, i) => `${i + 1}. ${typeLabels[t]}${t === currentType ? ' (current)' : ''}`).join('\n');
    const choice = prompt(`Select new field type:\n${options}\n\nEnter number (1-${types.length}):`);

    if (!choice) return;

    const index = parseInt(choice) - 1;
    if (isNaN(index) || index < 0 || index >= types.length) {
        alert('Invalid selection');
        return;
    }

    const newType = types[index];
    if (newType === currentType) return;

    try {
        const response = await fetch('/Tasks/UpdateCustomField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldId: fieldId,
                fieldType: newType
            })
        });

        if (!response.ok) throw new Error('Failed to update field type');

        // Clear cache and reload
        customFieldsCache = null;
        await loadFieldsList();
        await renderCustomFieldInputs('customFieldsContainer');

        console.log(`Field type changed to ${newType}`);

    } catch (error) {
        alert('Error changing field type: ' + error.message);
    }
}

// Load custom fields on page load
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOMContentLoaded - loading custom fields...');
    loadCustomFields();
});
