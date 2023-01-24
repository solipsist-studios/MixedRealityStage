using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Solipsist.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solipsist.ExperienceControl
{
    internal class GetAnchorIds
    {
        [FunctionName("GetAnchorIds")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "{expid}/getanchors")] HttpRequest req,
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

            List<string> anchors;
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition("select * from c");
                var resultSet = anchorContainer.GetItemQueryIterator<AnchorModel>(queryDefinition);

                anchors = await resultSet.ToAsyncEnumerable((AnchorModel a) => { return a.id.ToString(); }).ToListAsync();

                log.LogInformation("Finished parsing spatial anchors");
            }
            catch (CosmosException cosmos_DB_ex)
            {
                log.LogError(cosmos_DB_ex.Message);
                return new BadRequestResult();
            }

            JsonResult anchorsResult = new JsonResult(anchors);

            return new OkObjectResult(anchorsResult);
        }
    }
}
