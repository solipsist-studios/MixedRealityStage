using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Solipsist.Common;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    [StorageAccount("solipsistexperiencecatal")]
    public static class AddExperience
    {
        [FunctionName("AddExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.User, "post", Route = "expc/add")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("AddExperience HTTP function triggered.");

            try
            {
                log.LogInformation("Authenticated as user: {0}", ClaimsPrincipal.Current.Identity.Name);
            }
            catch (Exception ex)
            {
                log.LogError("Could not find user principal");
            }

            string resourceId = "https://solipsiststudios.onmicrosoft.com/experience-catalog";
            string[] scopes = new string[]
            {
                $"{resourceId}/experiences.read",
                $"{resourceId}/experiences.write"
            };

            // Try getting parameters from query string
            // TODO: make this more robust
            string experienceName = req.Query["name"];

            // Validate inputs
            if (experienceName == null) 
            {
                return new BadRequestResult();
            }

            // TODO: return a failure if an experience with the same name already exists

            // Connect to metadata db and query the experience metadata container
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            string ownerID = ""; ;
            try
            {
                var jsonToken = Utilities.GetTokenFromConfidentialClient(log, credential, scopes);
                ownerID = Utilities.GetUserIdentityFromToken(log, jsonToken);
            }
            catch (Exception ex)
            {
                log.LogError("Exception thrown in ListExperiences:");
                log.LogError($"{ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(ownerID))
            {
                ownerID = req.Query["ownerid"];
            }

            if (string.IsNullOrEmpty(ownerID))
            {
                log.LogError("Could not infer OwnerID.");
                return new BadRequestResult();
            }

            // Upload blob
            Stream myBlob;
            var file = req.Form.Files["payload"];
            string fileExt = Path.GetExtension(file.FileName);
            myBlob = file.OpenReadStream();

            return await RunLocal(log, credential, experienceName, ownerID, myBlob, fileExt);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, TokenCredential credential, string experienceName, string ownerID, Stream fileStream, string extension = "")
        {
            // Create a random ID
            // TODO: Use deterministic GUID generation
            string experienceID = System.Guid.NewGuid().ToString();

            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };

            // Set container to OwnerID
            BlobServiceClient storageClient = new BlobServiceClient((await Utilities.GetKeyVaultSecretAsync("StorageConnectionString", credential)).Value);
            BlobContainerClient blobClient = null;
            try
            {
                blobClient = storageClient.CreateBlobContainer(ownerID);
            }
            catch (Exception)
            {
                // This is expected behaviour, but there is no other way to check for the existence of a container... :(
                blobClient = storageClient.GetBlobContainerClient(ownerID);
            }

            BlobClient blob = blobClient.GetBlobClient(String.Format("{0}{1}", experienceID, extension));
            await blob.UploadAsync(fileStream);

            // Connect to metadata db and add this experience
            using CosmosClient client = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            var response = await client.GetContainer("experiences", "metadata").CreateItemAsync(
                new
                {
                    id = experienceID,
                    ownerID = ownerID,
                    name = experienceName,
                });

            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                log.LogError("Failed to create experience.  Status code:\n{0}", response.StatusCode);
            }

            string responseMessage = $"Successfully created the experience: {experienceName}";
            return response.StatusCode == System.Net.HttpStatusCode.Created ? new CreatedResult(experienceID, responseMessage) : new BadRequestResult();
        }
    }
}
