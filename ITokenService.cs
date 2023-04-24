using Microsoft.PowerPlatform.Dataverse.Client;
using System.Threading.Tasks;

namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Contracts
{
    /// <summary>
    /// 
    /// </summary>
    public interface IDataverseService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<string> GetTokenAsync();

        ServiceClient GetService();
    }
}
