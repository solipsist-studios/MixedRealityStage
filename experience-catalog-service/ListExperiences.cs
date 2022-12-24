using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Management.Network.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    public static class ListExperiences
    {
        [FunctionName("ListExperiences")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "list", Route = "expc/list")] HttpRequest req,
            ILogger log)
        {
            // Connect to metadata db and query the experience metadata container
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            string resourceId = "https://solipsiststudios.onmicrosoft.com/experience-catalog";

            //string[] scopes = new string[] { $"{resourceId}/.default" };
            string[] scopes = new string[]
            {
                $"{resourceId}/experiences.read",
                $"{resourceId}/experiences.write"
            };

            string token = "";
            try
            {
                token = credential.GetToken(new Azure.Core.TokenRequestContext(scopes), new System.Threading.CancellationToken()).Token;
            }
            catch (AuthenticationFailedException ex)
            {
                // Try getting the token from the header directly.
            }

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

            log.LogInformation("Full Token:\n{0}", jsonToken != null ? jsonToken.ToString() : "NOT FOUND");

            // TODO: Validate "aud" claim matches client ID
            string ownerID = jsonToken.Claims.First(c => c.Type == "oid").Value;

            // Input parameters are obtained from the route
            log.LogInformation($"GetExperiences HTTP function triggered for user {ownerID}");

            return await RunLocal(log, credential, ownerID);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, TokenCredential credential, string ownerID)
        {
            using CosmosClient client = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            var container = client.GetContainer("experiences", "metadata");

            QueryDefinition queryDefinition = new QueryDefinition(
                "select * from metadata m where m.ownerID = @ownerID")
                .WithParameter("@ownerID", ownerID);
            FeedIterator<ExperienceMetadata> resultSet = container.GetItemQueryIterator<ExperienceMetadata>(queryDefinition);

            // Format results
            var experiences = await resultSet.ToAsyncEnumerable().ToListAsync();
            JsonResult experienceResult = new JsonResult(experiences);

            return new OkObjectResult(experienceResult);
        }
    }
}
