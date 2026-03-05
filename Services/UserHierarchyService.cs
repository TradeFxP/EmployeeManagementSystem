using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;

namespace UserRoles.Services
{
    public class UserHierarchyService : IUserHierarchyService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public UserHierarchyService(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<Users>> GetAllManagersAsync()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .ToListAsync();

            var managers = new List<Users>();

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Manager"))
                {
                    managers.Add(u);
                }
            }

            return managers;
        }

        public async Task<List<string>> GetVisibleManagerIdsAsync(string rootManagerId)
        {
            var managers = await GetAllManagersAsync();

            var result = new List<string> { rootManagerId };

            void Traverse(string parentId)
            {
                var children = managers
                    .Where(m => m.ParentUserId == parentId)
                    .Select(m => m.Id)
                    .ToList();

                foreach (var childId in children)
                {
                    if (!result.Contains(childId))
                    {
                        result.Add(childId);
                        Traverse(childId);
                    }
                }
            }

            Traverse(rootManagerId);
            return result;
        }

        public List<Users> GetDescendantManagers(string managerId, List<Users> allManagers)
        {
            var result = new List<Users>();

            var children = allManagers
                .Where(m => m.ParentUserId == managerId)
                .ToList();

            foreach (var child in children)
            {
                result.Add(child);
                result.AddRange(GetDescendantManagers(child.Id, allManagers));
            }

            return result;
        }

        public OrgTreeNodeViewModel BuildOrgTree(Users root, List<Users> allManagers, List<Users> allUsers)
        {
            var node = new OrgTreeNodeViewModel
            {
                User = root
            };

            var childManagers = allManagers
                .Where(m => m.ParentUserId == root.Id)
                .ToList();

            foreach (var manager in childManagers)
            {
                node.Children.Add(BuildOrgTree(manager, allManagers, allUsers));
            }

            var childUsers = allUsers
                .Where(u => u.ParentUserId == root.Id)
                .ToList();

            foreach (var user in childUsers)
            {
                node.Children.Add(new OrgTreeNodeViewModel
                {
                    User = user
                });
            }

            return node;
        }

        public async Task CascadeMove(string subManagerId, string newParentId)
        {
            var children = await _context.Users
                .Where(u => u.ParentUserId == subManagerId || u.ManagerId == subManagerId)
                .ToListAsync();

            foreach (var child in children)
            {
                child.ParentUserId = subManagerId;
                child.ManagerId = subManagerId;
                await CascadeMove(child.Id, subManagerId);
            }
        }
    }
}
