using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Solipsist.Common;

namespace Solipsist.ExperienceControl
{
    internal class GetAnchorIds
    {
        [FunctionName("GetAnchorIds")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "{expid}/getanchors")] HttpRequest req,
            string expid,
            ILogger log)
        {
            // Connect to metadata db and query the experience metadata container
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            return await RunLocal(log, credential, expid);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, TokenCredential credential, string expID)
        {
            Microsoft.Azure.Cosmos.Container anchorContainer;

            using CosmosClient cosmosClient = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            anchorContainer = cosmosClient.GetContainer("experiences", expID);
            List<string> anchors = await GetAnchorIdsAsync(log, anchorContainer);

            JsonResult anchorsResult = new JsonResult(anchors);

            return new OkObjectResult(anchorsResult);
        }

        private static async Task<List<string>> GetAnchorIdsAsync(ILogger log, Container anchorContainer)
        {
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition("select * from c");
                var resultSet = anchorContainer.GetItemQueryIterator<string>(queryDefinition);

                List<string> anchorIds = await resultSet.ToAsyncEnumerable().ToListAsync();

                log.LogInformation("Finished parsing spatial anchors");
                return anchorIds;
            }
            catch (CosmosException cosmos_DB_ex)
            {
                log.LogError(cosmos_DB_ex.Message);
                return null;
            }
        }
    }
}
