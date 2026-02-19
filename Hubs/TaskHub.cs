using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace UserRoles.Hubs
{
    public class TaskHub : Hub
    {
        public async Task JoinTeamGroup(string teamName)
        {
            if (!string.IsNullOrEmpty(teamName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, teamName);
            }
        }

        public async Task LeaveTeamGroup(string teamName)
        {
            if (!string.IsNullOrEmpty(teamName))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, teamName);
            }
        }
    }
}
