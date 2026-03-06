using System.Collections.Generic;
using System.Text.Json;

namespace UserRoles.DTOs
{
    // ═══════ Lead Conversion ═══════
    public class LeadConversionDto
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? email { get; set; }
        public string? phone { get; set; }
        public string? formId { get; set; }
        public int? columnId { get; set; }
        public string? campaignName { get; set; }
        public string? adsetName { get; set; }
        public string? adName { get; set; }
        public string? metaCreatedAt { get; set; }
        public JsonElement? fields { get; set; }
    }

    // ═══════ Bulk Operations ═══════
    public class BulkAssignRequest
    {
        public List<int> TaskIds { get; set; }
        public string UserId { get; set; }
    }

    // ═══════ Column Management ═══════
    public class DeleteColumnRequest
    {
        public int columnId { get; set; }
    }

    // ═══════ Review & Archive ═══════
    public class ReviewTaskRequest
    {
        public int TaskId { get; set; }
        public bool Passed { get; set; }
        public string? ReviewNote { get; set; }
    }

    public class ArchiveRequest
    {
        public string TeamName { get; set; } = string.Empty;
    }

    // ═══════ Team Assignment ═══════
    public class AssignTaskToTeamRequest
    {
        public int TaskId { get; set; }
        public string TeamName { get; set; }
    }

    // ═══════ Custom Fields ═══════
    public class CreateFieldRequest
    {
        public string FieldName { get; set; } = string.Empty;
        public string FieldType { get; set; } = "Text";
        public bool IsRequired { get; set; } = false;
        public string? DropdownOptions { get; set; }
        public string? TeamName { get; set; }
    }

    public class UpdateFieldRequest
    {
        public int FieldId { get; set; }
        public string? FieldName { get; set; }
        public string? FieldType { get; set; }
        public bool? IsRequired { get; set; }
        public string? DropdownOptions { get; set; }
    }

    // ═══════ Team Settings ═══════
    public class UpdateTeamSettingsRequest
    {
        public string TeamName { get; set; }
        public bool IsPriorityVisible { get; set; }
        public bool IsDueDateVisible { get; set; }
        public bool IsTitleVisible { get; set; }
        public bool IsDescriptionVisible { get; set; }
    }

    // ═══════ Move Requests ═══════
    public class MoveRequestSubmitModel
    {
        public int TaskId { get; set; }
        public int ToColumnId { get; set; }
    }

    public class HandleMoveRequestModel
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Approved { get; set; }
        public string? AdminReply { get; set; }
    }
}
