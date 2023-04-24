using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Planeta.SARLAC_CRM_Integration.ApiNET6.DTOs;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Models;

namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Contracts
{
    /// <summary>
    /// 
    /// </summary>
    public interface ILeadService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="leadId"></param>
        /// <param name="brand"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(
            string token,
            string leadId,
            string brand);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<LeadPostResponse> CreateAsync(
            string token,
            JObject value);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="uriLead"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<LeadDto> UpdateLeadAsync(
           string token,
           string uriLead,
           JObject value);

        Task<LeadPostResponse> GetAsync(
            string token,
            string query,
            string field);

        Task<JObject> FillCRMFields(JObject json, string token, ProfileDto profile);

        Task<JObject> RetrieveInstitution(string token, ProfileDto profile, JObject json);

        Task<bool> ExistLeadWithJSON(string token, string json);
    }
}
