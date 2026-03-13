using System.Security.Claims;
using UserRoles.Models;
using UserRoles.ViewModels;

namespace UserRoles.Services
{
    /// <summary>
    /// Core task business-logic service.  Controllers should delegate
    /// all DB / permission / SignalR work to this service.
    /// </summary>
    public interface ITaskService
    {
        // ── CRUD ──────────────────────────────────────────────────
        Task<ServiceResult<TaskItem>> CreateTaskAsync(CreateTaskViewModel model, string userId);
        Task<ServiceResult> UpdateTaskAsync(UpdateTaskRequest model, ClaimsPrincipal principal, string userId);
        Task<ServiceResult> DeleteTaskAsync(int taskId, ClaimsPrincipal principal);
        Task<TaskItem?> GetTaskByIdAsync(int taskId, bool includeRelated = false);

        // ── Assignment ────────────────────────────────────────────
        Task<ServiceResult<AssignResult>> AssignTaskAsync(int taskId, string userId, ClaimsPrincipal principal, string currentUserId);
        Task<ServiceResult<int>> BulkAssignTasksAsync(List<int> taskIds, string userId, ClaimsPrincipal principal, string currentUserId);

        // ── Movement ──────────────────────────────────────────────
        Task<ServiceResult> MoveTaskAsync(int taskId, int columnId, ClaimsPrincipal principal, string userId);

        // ── Board Data ────────────────────────────────────────────
        Task<TeamBoardViewModel> BuildTeamBoardAsync(string team, string userId, IList<string> viewerRoles);
        Task<TeamBoardViewModel> GetQuickAssignModelAsync(string userId, IList<string> viewerRoles);
        Task AutoArchiveOldCompletedTasksAsync(string team, string userId);

        // ── Visibility helpers ────────────────────────────────────
        bool CanUserSeeTask(TaskItem task, string userId, IList<string> viewerRoles,
            Dictionary<string, IList<string>> userRolesMap, Dictionary<string, string?> hierarchyMap);
    }

    // ── Shared result types ──────────────────────────────────────
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int StatusCode { get; set; } = 200;

        public static ServiceResult Ok(string? msg = null) => new() { Success = true, Message = msg };
        public static ServiceResult Fail(string msg, int code = 400) => new() { Success = false, Message = msg, StatusCode = code };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; set; }

        public static ServiceResult<T> Ok(T data, string? msg = null) => new() { Success = true, Data = data, Message = msg };
        public new static ServiceResult<T> Fail(string msg, int code = 400) => new() { Success = false, Message = msg, StatusCode = code };
    }

    public class AssignResult
    {
        public string? AssignedTo { get; set; }
        public string? AssignedBy { get; set; }
        public string? AssignedAt { get; set; }
        public bool TeamMoved { get; set; }
        public string? NewTeam { get; set; }
    }
}
