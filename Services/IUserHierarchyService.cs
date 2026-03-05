using System.Collections.Generic;
using System.Threading.Tasks;
using UserRoles.Models;
using UserRoles.ViewModels;

namespace UserRoles.Services
{
    public interface IUserHierarchyService
    {
        Task<List<Users>> GetAllManagersAsync();
        Task<List<string>> GetVisibleManagerIdsAsync(string rootManagerId);
        List<Users> GetDescendantManagers(string managerId, List<Users> allManagers);
        OrgTreeNodeViewModel BuildOrgTree(Users root, List<Users> allManagers, List<Users> allUsers);
        Task CascadeMove(string subManagerId, string newParentId);
    }
}
