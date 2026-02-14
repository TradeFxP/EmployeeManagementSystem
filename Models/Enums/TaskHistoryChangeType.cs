namespace UserRoles.Models.Enums
{
    public enum TaskHistoryChangeType
    {
        Created = 0,
        Updated = 1,
        Assigned = 2,
        StatusChanged = 3,
        ColumnMoved = 4,
        FieldValueChanged = 5,
        PriorityChanged = 6,
        Deleted = 7,
        ReviewSubmitted = 8,
        ReviewPassed = 9,
        ReviewFailed = 10,
        ArchivedToHistory = 11
    }
}
