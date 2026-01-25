//// ================= INLINE EDIT REPORT =================
//window.openInlineEditReport = function (reportId, userId) {
//    const panel = document.getElementById("detailsPanel");
//    if (!panel) return;

//    panel.dataset.userid = userId;
//    panel.innerHTML = "<div class='text-muted'>Loading edit form...</div>";

//    fetch(`/Reports/EditReportPanel?reportId=${encodeURIComponent(reportId)}&userId=${encodeURIComponent(userId)}`)
//        .then(r => {
//            if (!r.ok) throw r;
//            return r.text();
//        })
//        .then(html => {
//            panel.innerHTML = html;
//        })
//        .catch(async err => {
//            let msg = "Failed to load edit form";
//            if (err instanceof Response) {
//                try { msg = await err.text(); } catch { }
//            }
//            panel.innerHTML = `<div class="text-danger">${msg}</div>`;
//        });
//};


// ================= EDIT REPORT INLINE =================
//window.editReportInline = function (reportId) {

//    const panel = document.getElementById("detailsPanel");
//    if (!panel) {
//        alert("Right panel not found");
//        return;
//    }

//    panel.innerHTML =
//        "<div class='text-muted p-3'>Loading edit form...</div>";

//    fetch(`/Reports/EditInlinePanel?id=${reportId}`)
//        .then(res => {
//            if (!res.ok) throw new Error();
//            return res.text();
//        })
//        .then(html => {
//            panel.innerHTML = html;
//        })
//        .catch(() => {
//            panel.innerHTML =
//                "<div class='text-danger p-3'>Failed to load edit form</div>";
//        });
//};


// ================= INLINE EDIT REPORT (GLOBAL) =================
window.openInlineEditReport = function (reportId) {

    const panel = document.getElementById("detailsPanel");
    if (!panel) {
        console.warn("detailsPanel not found");
        return;
    }

    const userId = "@ViewBag.TargetUserId";

    panel.dataset.userid = userId;
    panel.innerHTML = "<div class='text-muted p-3'>Loading edit form...</div>";

    fetch(`/Reports/EditReportPanel?reportId=${encodeURIComponent(reportId)}&userId=${encodeURIComponent(userId)}`)
        .then(r => {
            if (!r.ok) throw r;
            return r.text();
        })
        .then(html => {
            panel.innerHTML = html;
        })
        .catch(async err => {
            let msg = "Failed to load edit form";
            if (err instanceof Response) {
                try { msg = await err.text(); } catch { }
            }
            panel.innerHTML = `<div class="text-danger p-3">${msg}</div>`;
        });
};


// ================= DELETE REPORT INLINE =================
window.deleteReport = async function (id, userId) {

    if (!confirm("Delete this report?")) return;

    try {
        const res = await fetch('/Reports/DeleteInline', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ id }).toString(),
            credentials: 'same-origin'
        });

        if (!res.ok) {
            alert(await res.text());
            return;
        }

        // remove row
        const row = document.getElementById('report-row-' + id);
        if (row) row.remove();

        // reload reports panel
        if (typeof loadReports === "function") {
            setTimeout(() => loadReports(userId), 500);
        }

    } catch (e) {
        console.error(e);
        alert("Failed to delete report");
    }
};
