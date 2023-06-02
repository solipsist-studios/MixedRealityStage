using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Policy;

namespace Solipsist.ExperienceCatalog
{
    [StorageAccount("solipsistexperiencecatal")]
    public static class UpdateExperience
    {
        [FunctionName("UpdateExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.User, "post", Route = "expc/update")] HttpRequest req,
            ILogger log)
        {
            // Run "stop unity" command
            // Upload new package
            // Run "update" script

            //   access_token=`curl 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https%3A%2F%2Fstorage.azure.com%2F' -H Metadata:true | jq -r .access_token`
            //   curl https://solipsistexperiencecatal.blob.core.windows.net/41640163-5389-445d-b76e-cf06ea90af9d/6b529aa9-64ad-4e55-8d59-b8eda124ab0b -H "x-ms-version: 2017-11-09" -H "Authorization: Bearer $access_token" --output hello_stage.tar.gz



            // Run "Start unity" command
        }
    }
}
