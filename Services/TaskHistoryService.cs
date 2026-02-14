using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.Models.Enums;
using UserRoles.ViewModels;

namespace UserRoles.Services
{
    public class TaskHistoryService : ITaskHistoryService
    {
        private readonly AppDbContext _context;

        public TaskHistoryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogTaskCreated(int taskId, string userId)
        {
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.Created,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Details = "Task created"
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogTaskUpdated(int taskId, string userId, string fieldChanged, string oldValue, string newValue)
        {
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.Updated,
                FieldChanged = fieldChanged,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogColumnMove(int taskId, int fromColumnId, int toColumnId, string userId)
        {
            // Get the task to calculate time spent in previous column
            var task = await _context.TaskItems.FindAsync(taskId);
            int? timeSpentInSeconds = null;

            if (task != null && task.CurrentColumnEntryAt != default(DateTime))
            {
                var duration = DateTime.UtcNow - task.CurrentColumnEntryAt;
                timeSpentInSeconds = (int)duration.TotalSeconds;
            }

            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.ColumnMoved,
                FromColumnId = fromColumnId,
                ToColumnId = toColumnId,
                TimeSpentInSeconds = timeSpentInSeconds,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogAssignment(int taskId, string assignedToUserId, string assignedByUserId)
        {
            var assignedToUser = await _context.Users.FindAsync(assignedToUserId);
            
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.Assigned,
                NewValue = assignedToUser?.UserName ?? assignedToUserId,
                ChangedByUserId = assignedByUserId,
                ChangedAt = DateTime.UtcNow,
                Details = $"Assigned to {assignedToUser?.UserName ?? assignedToUserId}"
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogPriorityChange(int taskId, TaskPriority oldPriority, TaskPriority newPriority, string userId)
        {
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.PriorityChanged,
                FieldChanged = "Priority",
                OldValue = oldPriority.ToString(),
                NewValue = newPriority.ToString(),
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogCustomFieldChange(int taskId, string fieldName, string oldValue, string newValue, string userId)
        {
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.FieldValueChanged,
                FieldChanged = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogTaskDeleted(int taskId, string userId)
        {
            var history = new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.Deleted,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Details = "Task deleted"
            };

            _context.TaskHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        public async Task LogReviewSubmitted(int taskId, string userId)
        {
            _context.TaskHistories.Add(new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.ReviewSubmitted,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Details = "Task submitted for review"
            });
            await _context.SaveChangesAsync();
        }

        public async Task LogReviewPassed(int taskId, string userId, string? reviewNote)
        {
            _context.TaskHistories.Add(new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.ReviewPassed,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Details = string.IsNullOrWhiteSpace(reviewNote) ? "Review passed" : $"Review passed: {reviewNote}"
            });
            await _context.SaveChangesAsync();
        }

        public async Task LogReviewFailed(int taskId, string userId, string? reviewNote)
        {
            _context.TaskHistories.Add(new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.ReviewFailed,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Details = string.IsNullOrWhiteSpace(reviewNote) ? "Review failed" : $"Review failed: {reviewNote}"
            });
            await _context.SaveChangesAsync();
        }

        public async Task LogArchivedToHistory(int taskId, string userId)
        {
            _context.TaskHistories.Add(new TaskHistory
            {
                TaskId = taskId,
                ChangeType = TaskHistoryChangeType.ArchivedToHistory,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Details = "Task archived to history"
            });
            await _context.SaveChangesAsync();
        }

        public async Task<List<TaskHistoryDto>> GetTaskHistory(int taskId)
        {
            var history = await _context.TaskHistories
                .Include(h => h.ChangedByUser)
                .Include(h => h.FromColumn)
                .Include(h => h.ToColumn)
                .Where(h => h.TaskId == taskId)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new TaskHistoryDto
                {
                    Id = h.Id,
                    ChangeType = h.ChangeType,
                    FieldChanged = h.FieldChanged,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue,
                    FromColumnId = h.FromColumnId,
                    FromColumnName = h.FromColumn != null ? h.FromColumn.ColumnName : null,
                    ToColumnId = h.ToColumnId,
                    ToColumnName = h.ToColumn != null ? h.ToColumn.ColumnName : null,
                    TimeSpentInSeconds = h.TimeSpentInSeconds,
                    ChangedByUserId = h.ChangedByUserId,
                    ChangedByUserName = h.ChangedByUser.UserName ?? "Unknown",
                    ChangedAt = h.ChangedAt,
                    Details = h.Details
                })
                .ToListAsync();

            return history;
        }
    }
}
