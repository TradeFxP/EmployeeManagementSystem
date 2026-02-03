namespace UserRoles.Models
{
    public class UserTeam
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public Users User { get; set; }

        public string TeamName { get; set; } // Development | Testing | Sales
    }
}
