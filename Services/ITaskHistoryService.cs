using UserRoles.Models.Enums;
using UserRoles.ViewModels;

namespace UserRoles.Services
{
    public interface ITaskHistoryService
    {
        Task LogTaskCreated(int taskId, string userId);
        Task LogTaskUpdated(int taskId, string userId, string fieldChanged, string oldValue, string newValue);
        Task LogColumnMove(int taskId, int fromColumnId, int toColumnId, string userId);
        Task LogAssignment(int taskId, string assignedToUserId, string assignedByUserId);
        Task LogPriorityChange(int taskId, TaskPriority oldPriority, TaskPriority newPriority, string userId);
        Task LogCustomFieldChange(int taskId, string fieldName, string oldValue, string newValue, string userId);
        Task LogTaskDeleted(int taskId, string userId);
        Task LogReviewSubmitted(int taskId, string userId);
        Task LogReviewPassed(int taskId, string userId, string? reviewNote);
        Task LogReviewFailed(int taskId, string userId, string? reviewNote);
        Task LogArchivedToHistory(int taskId, string userId);
        Task<List<TaskHistoryDto>> GetTaskHistory(int taskId);
    }
}
