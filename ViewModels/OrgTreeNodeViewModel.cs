using UserRoles.Models;

namespace UserRoles.ViewModels
{
    // Represents ONE node in the organization tree
    // Can be Admin / Manager / Sub-Manager / User
    public class OrgTreeNodeViewModel
    {
        // Current user
        public Users User { get; set; } = default!;

        // Children under this user (managers or users)
        public List<OrgTreeNodeViewModel> Children { get; set; } = new();
    }
}
