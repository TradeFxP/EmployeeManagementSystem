// index_ui.js - UI functions for Index view

function toggleTeamTasks() {
    const dropdown = document.getElementById("teamTasksDropdown");
    const arrow = document.getElementById("teamArrow");

    if (dropdown.style.display === "none") {
        dropdown.style.display = "block";
        arrow.innerText = "▲";
    } else {
        dropdown.style.display = "none";
        arrow.innerText = "▼";
    }
}


function toggleMyTasks() {
    const dropdown = document.getElementById("myTasksDropdown");
    const arrow = document.getElementById("myTasksArrow");

    if (!dropdown) return;

    const isOpen = dropdown.style.display === "block";
    dropdown.style.display = isOpen ? "none" : "block";
    arrow.innerText = isOpen ? "▼" : "▲";
}

// ===== PROJECT PANEL =====
function toggleProjectPanel() {
    const dropdown = document.getElementById("projectDropdown");
    const arrow = document.getElementById("projectArrow");

    if (!dropdown) return;

    const isOpen = dropdown.style.display === "block";
    dropdown.style.display = isOpen ? "none" : "block";
    arrow.innerText = isOpen ? "▼" : "▲";

    if (!isOpen) {
        loadProjectList();
    }
}

function loadProjectList() {
    fetch('/Project/GetProjects')
        .then(res => res.json())
        .then(projects => {
            const container = document.getElementById('projectList');
            if (projects.length === 0) {
                container.innerHTML = '<div class="text-muted ps-4 py-2">No projects yet</div>';
                return;
            }

            container.innerHTML = projects.map(p => `
                <div class="d-flex align-items-center justify-content-between group-hover-container px-0">
                    <button type="button"
                            class="list-group-item list-group-item-action bg-dark text-white project-btn flex-grow-1 text-start border-0 py-2 ps-4"
                            data-project-id="${p.id}"
                            onclick="loadProjectBoard(${p.id})">
                        <i class="bi bi-folder2 me-2"></i> ${p.name}
                    </button>
                    <button class="btn btn-sm btn-link text-danger p-0 me-2" 
                            style="opacity:0.5;"
                            onmouseover="this.style.opacity=1"
                            onmouseout="this.style.opacity=0.5"
                            onclick="event.stopPropagation(); deleteProject(${p.id}, '${p.name}')" 
                            title="Delete Project">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            `).join('');
        })
        .catch(() => {
            document.getElementById('projectList').innerHTML =
                '<div class="text-danger ps-4 py-2">Failed to load</div>';
        });
}

function loadProjectBoard(projectId) {
    const container = document.getElementById('taskBoardContainer');
    container.innerHTML = "<div class='text-muted p-3'>Loading project...</div>";

    // Clear team button highlights
    document.querySelectorAll('.team-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.project-btn').forEach(b => b.classList.remove('active'));
    document.querySelector(`[data-project-id="${projectId}"]`)?.classList.add('active');

    fetch(`/Project/Board/${projectId}`)
        .then(res => {
            if (!res.ok) throw new Error("Failed to load project board");
            return res.text();
        })
        .then(html => {
            container.innerHTML = html;

            // Execute scripts from the dynamically loaded content
            const scripts = container.querySelectorAll('script');
            scripts.forEach(script => {
                const newScript = document.createElement('script');
                if (script.src) {
                    newScript.src = script.src;
                } else {
                    newScript.textContent = script.textContent;
                }
                document.body.appendChild(newScript);
                // Remove after execution to avoid duplicates
                setTimeout(() => newScript.remove(), 100);
            });
        })
        .catch(err => {
            container.innerHTML = "<div class='text-danger'>Error loading project</div>";
            console.error(err);
        });
}

function openCreateProjectModal() {
    document.getElementById('newProjectName').value = '';
    document.getElementById('newProjectDescription').value = '';
    new bootstrap.Modal(document.getElementById('createProjectModal')).show();
}

function submitCreateProject() {
    const data = {
        name: document.getElementById('newProjectName').value.trim(),
        description: document.getElementById('newProjectDescription').value.trim()
    };

    if (!data.name) {
        alert('Project name is required');
        return;
    }

    fetch('/Project/CreateProject', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    })
        .then(res => {
            if (!res.ok) throw new Error('Failed to create project');
            return res.json();
        })
        .then(result => {
            bootstrap.Modal.getInstance(document.getElementById('createProjectModal'))?.hide();
            loadProjectList();
            loadProjectBoard(result.id);
        })
        .catch(err => alert(err.message));
}
