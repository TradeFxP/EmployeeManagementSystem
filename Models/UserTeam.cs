namespace UserRoles.Models
{
    public class UserTeam
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public Users? User { get; set; }
        public string TeamName { get; set; } = string.Empty;
    }
}
