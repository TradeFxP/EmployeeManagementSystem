namespace UserRoles.ViewModels
{
    /// <summary>
    /// Request model used when admin adds a new column
    /// </summary>
    public class AddColumnRequest
    {
        // Team name (Development / Testing / Sales)
        public string Team { get; set; }

        // Name of the new column (e.g. QA Review)
        public string ColumnName { get; set; }
    }
}
