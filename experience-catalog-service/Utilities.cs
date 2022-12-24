using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Management.Network.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
//using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace Solipsist.ExperienceCatalog
{
    internal class Utilities
    {
        // TODO: Make this work for both local settings and Key Storage
        // TODO2: Replace this with Identity based auth
        public static string GetBlobStorageConnectionString(string name)
        {
            ConnectionStringSettings conStrSetting = ConfigurationManager.ConnectionStrings[$"ConnectionStrings:{name}"];
            string conStr = conStrSetting != null ?
                conStrSetting.ConnectionString :
                // Azure Functions App Service naming convention
                Environment.GetEnvironmentVariable($"BlobStorageConnectionString");

            return conStr;
        }

        public static async Task<string> GetCurrentUserIdentityAsync(ILogger log, TokenCredential credential)
        {
            string resourceId = "https://solipsiststudios.onmicrosoft.com/experience-catalog";
            //string[] scopes = new string[] { $"{resourceId}/.default" };
            string[] scopes = new string[]
            {
                $"{resourceId}/experiences.read",
                $"{resourceId}/experiences.write"
            };

            string token = "";

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

            log.LogInformation("Full Token:\n{0}", jsonToken != null ? jsonToken.ToString() : "NOT FOUND");

            // TODO: Validate "aud" claim matches client ID
            return jsonToken.Claims.First(c => c.Type == "oid").Value;
        }

        public static async Task<KeyVaultSecret> GetKeyVaultSecretAsync(string secretName, TokenCredential credential)
        {
            string keyVaultUrl = "https://experience-catalog-kv.vault.azure.net/";

            var client = new SecretClient(vaultUri: new Uri(keyVaultUrl), credential);

            var secretResponse = await client.GetSecretAsync(secretName);

            return secretResponse.Value;
        }

        public static async Task GetOrCreateResource()
        {

        }
    }
}
