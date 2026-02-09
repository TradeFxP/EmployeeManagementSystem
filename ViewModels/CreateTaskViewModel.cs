namespace UserRoles.ViewModels
{
    public class CreateTaskViewModel
    {

        public int ColumnId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Optional: Link task to a project for hierarchical ID generation
        public int? ProjectId { get; set; }
    }
}
