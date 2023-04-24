using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Configuration;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Extensions;

namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Implementations
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpClientService//: IHttpClientService
    {
        /// <summary>
        /// 
        /// </summary>
        protected ILogger Logger;

        /// <summary>
        /// 
        /// </summary>
        public HttpClient HttpClient;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="options"></param>
        public HttpClientService(
            ILoggerFactory loggerFactory, 
            IOptions<AppConfig> options)
        {
            Logger = loggerFactory.CreateLogger(GetType().Namespace);

            var httpHandler = new WinHttpHandler
            {
                SslProtocols = SslProtocols.Tls12
            };

            HttpClient = new HttpClient(httpHandler);
            
            var baseAddress = options?.Value?.Dynamics365?.Resource;

            if (string.IsNullOrEmpty(baseAddress)) return;

            HttpClient.BaseAddress = new Uri(baseAddress + "api/data/v9.2/");
            HttpClient.Timeout = new TimeSpan(0, 2, 0);
            HttpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            HttpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
            HttpClient.DefaultRequestHeaders.Add("keep-alive", "0");
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="method"></param>
        /// <param name="query"></param>
        /// <param name="formatted"></param>
        /// <param name="maxPageSize"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendRequestAsync(
            string token,
            HttpMethod method,
            string query,
            bool formatted = false,
            int maxPageSize = 10,
            StringContent content = null)
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new HttpRequestMessage(method, query);

            //request.Headers.Add("Prefer", "odata.maxpagesize=" + maxPageSize.ToString());

            if (content != null)
            {
                request.Content = content;
            }

            if (formatted)
            {
                request.Headers.Add("Prefer",
                    "odata.include-annotations=OData.Community.Display.V1.FormattedValue");
            }

            HttpResponseMessage response = null;

            try
            {
                response = await HttpClient.SendAsync(request);
            }
            catch (Exception exception)
            {
                Logger.LogError("Was thrown an exception {0}", exception.ToString());

                Logger.LogDebug("Going to response with a json result");

                if (response != null) response.StatusCode = HttpStatusCode.InternalServerError;
            }

            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="entityType"></param>
        /// <param name="entity"></param>
        /// <param name="metodo"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendAsJsonAsync(
            string token,
            string entityType,
            JObject entity,
            HttpMethod metodo)
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return await HttpClient.SendAsJsonAsync(metodo, entityType, entity);
        }

        public async Task<HttpResponseMessage> GetAsync(
            string token, string query)
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return await HttpClient.GetAsync(query);
        }

    }
}
