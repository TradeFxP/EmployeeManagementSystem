// ================= VIEW =================
function viewReport(reportId) {
    window.location.href = "/Reports/Details/" + reportId;
}

// ================= EDIT =================
//function editReportInline(reportId) {

//    const panel = document.getElementById("detailsPanel");
//    if (!panel) {
//        alert("Details panel not found");
//        return;
//    }

//    panel.innerHTML = "<div class='p-3 text-muted'>Loading...</div>";

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
//                "<div class='p-3 text-danger'>Failed to load edit form</div>";
//        });
//}

// ================= DELETE =================
function deleteReport(reportId) {

    if (!confirm("Are you sure you want to delete this report?"))
        return;

    fetch("/Reports/DeleteInline", {
        method: "POST",
        headers: {
            "Content-Type": "application/x-www-form-urlencoded"
        },
        body: new URLSearchParams({ id: reportId }).toString()
    })
        .then(res => {
            if (!res.ok) throw new Error();
            const row = document.getElementById("report-row-" + reportId);
            if (row) row.remove();
        })
        .catch(() => {
            alert("Delete failed");
        });
}

// ================= LOAD REPORTS PANEL =================
// Fetches the user reports partial and renders it into the #detailsPanel element.
window.loadReports = function (userId) {
    if (!userId) return;

    const panel = document.getElementById("detailsPanel");
    if (!panel) return;

    panel.innerHTML = "<div class='p-3 text-muted'>Loading reports...</div>";

    fetch(`/Reports/UserReportsPanel?userId=${encodeURIComponent(userId)}`)
        .then(res => {
            if (!res.ok) throw new Error();
            return res.text();
        })
        .then(html => {
            panel.innerHTML = html;
        })
        .catch(() => {
            panel.innerHTML = "<div class='p-3 text-danger'>Failed to load reports</div>";
        });
};

