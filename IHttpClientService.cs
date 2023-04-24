using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Contracts
{
    /// <summary>
    /// 
    /// </summary>
    public interface IHttpClientService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="method"></param>
        /// <param name="query"></param>
        /// <param name="formatted"></param>
        /// <param name="maxPageSize"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> SendRequestAsync(
            string token,
            HttpMethod method,
            string query,
            bool formatted = false,
            int maxPageSize = 10);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="entityType"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task<HttpResponseMessage> SendAsJsonAsync(
                    string token,
                    string entityType,
                    JObject entity);
    }
}
