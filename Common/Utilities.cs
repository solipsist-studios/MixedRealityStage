using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace Solipsist.Common
{
    public class Utilities
    {
        public static JwtSecurityToken GetJwtFromString(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            return jsonToken;
        }

        public static JwtSecurityToken GetTokenFromConfidentialClient(ILogger log, TokenCredential credential, string[] scopes)
        {
            string token = "";
            try
            {
                token = credential.GetToken(new Azure.Core.TokenRequestContext(scopes), new System.Threading.CancellationToken()).Token;
            }
            catch (AuthenticationFailedException ex)
            {
                // Try getting the token from the header directly.
            }

            var jsonToken = GetJwtFromString(token);

#if DEBUG
            // Only print this info in debug mode!!
            log.LogDebug("Full Token:\n{0}", jsonToken != null ? jsonToken.ToString() : "NOT FOUND");
#endif

            return jsonToken;
        }

        public static string GetUserIdentityFromToken(ILogger log, JwtSecurityToken jsonToken)
        {
            // TODO: Validate "aud" claim matches client ID
            return jsonToken == null ? "" : jsonToken.Claims.First(c => c.Type == "oid").Value;
        }

        public static async Task<KeyVaultSecret> GetKeyVaultSecretAsync(string secretName, TokenCredential credential)
        {
            string keyVaultUrl = "https://experience-catalog-kv.vault.azure.net/";

            var client = new SecretClient(vaultUri: new Uri(keyVaultUrl), credential);

            var secretResponse = await client.GetSecretAsync(secretName);

            return secretResponse.Value;
        }
    }
}