using Microsoft.Extensions.Options;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Configuration;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Contracts;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.PowerPlatform.Dataverse.Client;
using static System.Net.WebRequestMethods;
using System.Security.Policy;

namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Implementations
{
    /// <summary>
    /// 
    /// </summary>
    public class DataverseService : IDataverseService
    {
        #region Fields

        /// <summary>
        /// 
        /// </summary>
        private readonly AuthenticationConfiguration _authenticationConfiguration;

        /// <summary>
        /// 
        /// </summary>
        private readonly ILogger _logger;

        HttpClient _httpClient = new HttpClient(); // TODO: Change this later...

        private KeyVaultSecret _ateneaClientId;
        private KeyVaultSecret _ateneaClientSecret;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="options"></param>
        public DataverseService(
            ILoggerFactory loggerFactory,
             IOptions<AppConfig> options)
        {
            _logger = loggerFactory.CreateLogger(GetType().Namespace);
            _authenticationConfiguration = options?.Value?.Authentication;
        }


        public async Task<string> GetTokenAsync()
        {
            var token = string.Empty;

            try
            {
                //this.GetSecretsUsingManageIdentity();

                string serviceUrl = Environment.GetEnvironmentVariable("CrmUrl");
                string clientid = Environment.GetEnvironmentVariable("ClientId");
                string clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
                string tenantId = Environment.GetEnvironmentVariable("TenantId");

                AuthenticationContext authContext = new AuthenticationContext("https://login.microsoftonline.com/" + tenantId);
                //ClientCredential credential = new ClientCredential(_ateneaClientId.Value, _ateneaClientSecret.Value);
                ClientCredential credential = new ClientCredential(clientid, clientSecret);

                AuthenticationResult result = await authContext.AcquireTokenAsync(serviceUrl, credential);

                return result.AccessToken;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error getting token. Message: {e.ToString()}");
            }

            return token;
        }

        public ServiceClient GetService()
        {

            string crmURL = Environment.GetEnvironmentVariable("CrmUrl");
            string clientid = Environment.GetEnvironmentVariable("ClientId");
            string clientSecret = Environment.GetEnvironmentVariable("ClientSecret");

            var connectionString = string.Format(@"
            AuthType = ClientSecret;
            url = {0};
            ClientId ={1};
            ClientSecret ={2}
            ",
            crmURL,
            clientid,
            clientSecret);
            // Create a Dataverse service client using the default connection string.
            return new ServiceClient(connectionString);
        }

        //private async void GetSecretsUsingManageIdentity()
        //{
        //    try
        //    {
        //        var keyVaultName = "athd365keyvaultdev";
        //        var kvUri = $"https://{keyVaultName}.vault.azure.net";

        //        var keyVaultClient = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
        //        _ateneaClientId = keyVaultClient.GetSecret("AteneaDynamicsClientIDT3");
        //        _ateneaClientSecret = keyVaultClient.GetSecret("AteneaDynamicsClientSecretT3");
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception("Error retrieving AzureKeyVault Secrets: " + ex.Message);
        //    }
        //}
    }
}
