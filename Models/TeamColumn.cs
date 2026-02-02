namespace UserRoles.Models
{
    public class TeamColumn
    {
        public int Id { get; set; }

        // Development / Testing / Sales
        public string TeamName { get; set; }

        // ToDo, Doing, Review, Complete
        public string ColumnName { get; set; }

        // Order in board (left → right)
        public int Order { get; set; }
    }
}
