using System.Collections.Generic;
using System.Threading.Tasks;
using UserRoles.Models;

namespace UserRoles.Services
{
    public interface IFacebookLeadsService
    {
        Task<List<FacebookLeadDto>> FetchLeadsAsync(string? formId = null);
    }
}
