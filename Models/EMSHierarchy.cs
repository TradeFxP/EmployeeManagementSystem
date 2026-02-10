namespace UserRoles.Models
{
    public class EMSHierarchy
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public int? ParentId { get; set; }
    }
}
