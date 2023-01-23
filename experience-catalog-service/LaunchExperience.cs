using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Solipsist.Common;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    public static class LaunchExperience
    {
        private static Microsoft.Azure.Cosmos.Container metadataContainer;

        [FunctionName("LaunchExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expc/launch")] HttpRequest req,
            ILogger log)
        {
            string expID = req.Query["expid"];
            string location = string.IsNullOrEmpty(req.Query["loc"]) ? "EastUS2" : req.Query["loc"];
            AzureLocation azLocation = new AzureLocation(location);

            // TODO: admin name and password should be required values
            string adminUsername = string.IsNullOrEmpty(req.Query["adminusername"]) ? "solipsistadmin" : req.Query["adminusername"];
            string adminPassword = string.IsNullOrEmpty(req.Query["adminpassword"]) ? "solipsist4ever!" : req.Query["adminpassword"];

            log.LogInformation($"LaunchExperience HTTP function triggered for id: {expID}");

            // Connect to metadata db and query the experience metadata container
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            return await RunLocal(log, credential, azLocation, expID, adminUsername, adminPassword);
        }

        private static async Task<ExperienceMetadata> GetExperienceCatalogStateAsync(ILogger log, string expID)
        {
            QueryDefinition queryDefinition = new QueryDefinition(
                "select * from metadata m where m.id = @expID")
                .WithParameter("@expID", expID);

            var resultSet = metadataContainer.GetItemQueryIterator<ExperienceMetadata>(queryDefinition).ToAsyncEnumerable();
            ExperienceMetadata experience = await resultSet.FirstAsync();
            if (experience == null)
            {
                log.LogError("!!!!!!!!ERROR: No experience found with ID {0}!!!!!!!!", expID);
            }

            return experience;
        }

        private static async Task<IActionResult> SetExperienceCatalogStateAsync(ILogger log, string expID, ExperienceState state)
        {
            var experience = await GetExperienceCatalogStateAsync(log, expID);

            if (experience == null) 
            { 
                return new UnprocessableEntityResult(); 
            }
            
            experience.state = state;
            var response = await metadataContainer.UpsertItemAsync(experience);
            return response.StatusCode == System.Net.HttpStatusCode.OK ? new OkObjectResult(experience) : new UnprocessableEntityObjectResult(experience);
        }

        // TODO: This function should be atomic
        // TODO 2: These functions are doing too much and should be refactored
        public static async Task<IActionResult> RunLocal(ILogger log, TokenCredential credential, AzureLocation location, string expID, string adminUsername, string adminPassword)
        {
            // Authenticate
            log.LogInformation("--------Start creating ARM client with auth token--------");
            ArmClient client = new ArmClient(credential);
            log.LogInformation("--------End creating ARM client--------");

            log.LogInformation("--------Start fetching storage clients--------");
            // Connect to metadata db and query the experience metadata container
            using CosmosClient cosmosClient = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            metadataContainer = cosmosClient.GetContainer("experiences", "metadata");

            IActionResult experienceResult = await SetExperienceCatalogStateAsync(log, expID, ExperienceState.Starting);
            ExperienceMetadata experience = null;
            if (experienceResult is ObjectResult)
            {
                experience = ((ObjectResult)experienceResult).Value as ExperienceMetadata;
            }

            if (experience == null || experience.state != ExperienceState.Starting)
            {
                return new UnprocessableEntityResult();
            }

            log.LogInformation("--------End fetching storage clients--------");

            // Get / Create Resource Group
            log.LogInformation("--------Start fetching Resource Group--------");
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            var createRGJob = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, expID, new ResourceGroupData(location));
            ResourceGroupResource resourceGroup = createRGJob.Value;
            log.LogInformation("--------End fetching Resource Group--------");

            // Create / start VM
            log.LogInformation("--------Start fetching Virtual Machine--------");
            VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
            VirtualMachineResource vmResource = null;
            ArmDeploymentResource deployment = null;
            if (!vmCollection.Exists(experience.name))
            {
                deployment = await CreateVMWithBicep(log, credential, experience.ownerID, expID, resourceGroup, vmCollection, experience.name, location, adminUsername, adminPassword);
            }

            vmResource = await vmCollection.GetAsync(experience.name);

            if (vmResource == null)
            {
                log.LogError("Failed to fetch VM Resource");
                return new UnprocessableEntityResult();
            }

            // TODO: Validate every resource -- probably via ARM or Bicep

            log.LogInformation("--------End fetching Virtual Machine--------");

            //foreach (var nicConfig in vmResource.Data.NetworkProfile.NetworkInterfaceConfigurations)
            //{
            //    if (nicConfig.Primary ?? false)
            //    {
            //        foreach (var ip in nicConfig.IPConfigurations)
            //        {
            //            log.LogInformation("VM IP Config: {0}", ip.PublicIPAddressConfiguration.ToString());
            //        }
            //    }
            //}

            log.LogInformation("--------Start fetching Virtual Machine--------");

            // Start the VM and update the Experience state
            //vmResource.PowerOn(WaitUntil.Started);

            JsonResult vmResult = new JsonResult(vmResource != null ? vmResource : deployment);

            return new OkObjectResult(vmResult);
        }

        private static async Task<ArmDeploymentResource> CreateVMWithBicep(ILogger log, TokenCredential credential, string ownerID, string expID, ResourceGroupResource resourceGroup, VirtualMachineCollection vmCollection, string vmName, AzureLocation location, string adminUsername, string adminPassword)
        {
            log.LogInformation("Start Creating VM Resources");
            string templatePath = Path.Combine(".", "Templates", "vm-template.json");
            string templateContent = File.ReadAllText(templatePath).TrimEnd();

            ArmDeploymentContent deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            {
                Template = BinaryData.FromString(templateContent),
                Parameters = BinaryData.FromObjectAsJson(new
                {
                    ownerId = new { value = ownerID },
                    experienceId = new { value = expID },
                    experienceName = new { value = vmName },
                    location = new { value = location.ToString() },
                    adminUsername = new { value = adminUsername },
                    adminPassword = new { value = adminPassword }
                })
            });

            var deploymentJob = await resourceGroup.GetArmDeployments().CreateOrUpdateAsync(WaitUntil.Completed, "vm-resource-deployment", deploymentContent);
            ArmDeploymentResource resource = null;
            ExperienceState newState = ExperienceState.Starting;

            if (deploymentJob == null || deploymentJob.Value == null)
            {
                log.LogError(String.Format("Failed deployment {0}", deploymentJob.Id));
                newState = ExperienceState.Stopped;
            }
            else
            {
                resource = deploymentJob.Value;
                newState = ExperienceState.Running;
            }

            await SetExperienceCatalogStateAsync(log, expID, newState);
            return resource;
        }
    }
}
