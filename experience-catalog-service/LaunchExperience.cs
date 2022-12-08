using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.EventGrid;
//using Azure.ResourceManager.EventGrid.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters.Internal;
using Microsoft.Azure.Cosmos;
//using Microsoft.Azure.Management.EventGrid;
//using Microsoft.Azure.Management.EventGrid.Models;
using Microsoft.Azure.Management.Network.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    public static class LaunchExperience
    {
        static readonly string tenantId = "d5f06f52-0502-420b-8324-b77ca4aa68dd";

        [FunctionName("LaunchExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expc/launch")] HttpRequest req,
            ILogger log)
        {
            string storageConnectionString = req.Query["storageconnectionstring"];
            string cosmosConnectionString = req.Query["cosmosconnectionstring"];
            string expID = req.Query["expid"];
            string location = string.IsNullOrEmpty(req.Query["loc"]) ? "EastUS2" : req.Query["loc"];
            AzureLocation azLocation = new AzureLocation(location);
            string adminUsername = string.IsNullOrEmpty(req.Query["adminusername"]) ? "jsipko" : req.Query["adminusername"];
            string adminPassword = string.IsNullOrEmpty(req.Query["adminpassword"]) ? "solipsist4ever!" : req.Query["adminpassword"];

            log.LogInformation($"LaunchExperience HTTP function triggered for id: {expID}");

            return await RunLocal(log, azLocation, storageConnectionString, cosmosConnectionString, expID, adminUsername, adminPassword);
        }

        private static async Task<IActionResult> SetExperienceCatalogState(ILogger log, Microsoft.Azure.Cosmos.Container metadataContainer, string expID, ExperienceState state)
        {
            QueryDefinition queryDefinition = new QueryDefinition(
                "select * from metadata m where m.id = @expID")
                .WithParameter("@expID", expID);
            var resultSet = metadataContainer.GetItemQueryIterator<ExperienceMetadata>(queryDefinition).ToAsyncEnumerable();
            ExperienceMetadata experience = await resultSet.FirstAsync();
            if (experience == null) 
            {
                log.LogError("!!!!!!!!ERROR: No experience found with ID {0}!!!!!!!!", expID);
                return new NotFoundResult();
            }

            if (experience.state != ExperienceState.Stopped)
            {
                log.LogWarning("Experience {0} could not be started from state {1}", expID, experience.state);
                return new UnprocessableEntityResult();
            }

            experience.state = state;
            var response = await metadataContainer.UpsertItemAsync(experience);
            return response.StatusCode == System.Net.HttpStatusCode.OK ? new OkObjectResult(experience) : new UnprocessableEntityObjectResult(experience);
        }

        // TODO: This function should be atomic
        // TODO 2: These functions are doing too much and should be refactored
        public static async Task<IActionResult> RunLocal(ILogger log, AzureLocation location, string? storageConnectionString, string? cosmosConnectionString, string expID, string adminUsername, string adminPassword)
        {
            // Connect to metadata db and query the experience metadata container
            using CosmosClient cosmosClient = new(connectionString: cosmosConnectionString);
            var metadataContainer = cosmosClient.GetContainer("experiences", "metadata");

            IActionResult experienceResult = await SetExperienceCatalogState(log, metadataContainer, expID, ExperienceState.Starting);
            ExperienceMetadata experience = null;
            if (experienceResult is ObjectResult)
            {
                experience = ((ObjectResult)experienceResult).Value as ExperienceMetadata;
            }

            if (experience == null || experience.state != ExperienceState.Starting)
            {
                return new UnprocessableEntityResult();
            }

            // Get connection strings
            storageConnectionString = storageConnectionString ?? Utilities.GetBlobStorageConnectionString("ExperienceStorage");
            cosmosConnectionString = cosmosConnectionString ?? Environment.GetEnvironmentVariable("CosmosDBConnectionString");

            // Authenticate
            log.LogInformation("--------Start creating ARM client with auth token--------");
            DefaultAzureCredential credential = new DefaultAzureCredential();
            string subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            // you can also use `new ArmClient(credential)` here, and the default subscription will be the first subscription in your list of subscription
            ArmClient client = new ArmClient(credential, subscriptionId);
            log.LogInformation("--------End creating ARM client--------");

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
            if (vmCollection.Exists(experience.name))
            {
                vmResource = await vmCollection.GetAsync(experience.name);
            }
            else
            {
                deployment = await CreateVMWithBicep(log, credential, subscriptionId, expID, resourceGroup, vmCollection, experience.name, location, adminUsername, adminPassword);
            
                // Also create the VM monitor logic

                //await CreateVMLogic(log, expID, )
            }

            // TODO: Validate every resource -- probably via ARM or Bicep

            log.LogInformation("--------End fetching Virtual Machine--------");
            log.LogInformation("VM ID: " + vmResource.Id);

            log.LogInformation("--------Start fetching Virtual Machine--------");

            // Start the VM and update the Experience state
            vmResource.PowerOn(WaitUntil.Started);

            JsonResult vmResult = new JsonResult(vmResource != null ? vmResource : deployment);

            return new OkObjectResult(vmResult);
        }

        private static async Task<ArmDeploymentResource> CreateVMWithBicep(ILogger log, TokenCredential credential, string ownerID, string expID, ResourceGroupResource resourceGroup, VirtualMachineCollection vmCollection, string vmName, AzureLocation location, string adminUsername, string adminPassword)
        {
            log.LogInformation("Start Creating VM Resources");

            ArmDeploymentContent deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            {
                Template = BinaryData.FromStream(File.Create(Path.Combine(".", "Templates", "vm-template.json"))),
                Parameters = BinaryData.FromObjectAsJson(new
                {
                    ownerId = new
                    {
                        value = ownerID
                    },
                    experienceId = new
                    {
                        value = expID
                    },
                    experienceName = new
                    {
                        value = vmName
                    },
                    location = new
                    {
                        value = location
                    }
                })
            });

            var deploymentJob = await resourceGroup.GetArmDeployments().CreateOrUpdateAsync(WaitUntil.Completed, "vm-resource-deployment", deploymentContent);
            return deploymentJob.Value;
        }

        private static async Task<VirtualMachineResource> CreateVirtualMachine(ILogger log, TokenCredential credential, string subscriptionID, string expID, ResourceGroupResource resourceGroup, VirtualMachineCollection vmCollection, string vmName, AzureLocation location, string adminUsername, string adminPassword)
        {
            log.LogInformation("--------Start creating IP Address--------");
            string ipAddressName = String.Format("{0}-ip", vmName);
            PublicIPAddressData ipAddressData = new PublicIPAddressData()
            {
                PublicIPAddressVersion = NetworkIPVersion.IPv4,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
                Location = location,
            };

            var ipAddressJob = await resourceGroup.GetPublicIPAddresses().CreateOrUpdateAsync(WaitUntil.Completed, ipAddressName, ipAddressData);
            PublicIPAddressResource ipAddress = ipAddressJob.Value;
            log.LogInformation("--------End creating IP Address--------");

            log.LogInformation("--------Start creating Virtual Network--------");
            string vnetName = String.Format("{0}-vnet", vmName);
            string subnetName = String.Format("{0}-subnet", vmName);
            VirtualNetworkData vnetData = new VirtualNetworkData()
            {
                Location = location.Name,
                AddressPrefixes = { "10.0.0.0/16", },
                Subnets = { new SubnetData() { Name = subnetName, AddressPrefix = "10.0.0.0/24", } }
            };

            var vnetJob = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetData);
            VirtualNetworkResource vnet = vnetJob.Value;
            log.LogInformation("--------End creating Virtual Network--------");

            log.LogInformation("--------Start creating Network Interface--------");
            string nicName = String.Format("{0}-nic", vmName);
            NetworkInterfaceData nicData = new NetworkInterfaceData()
            {
                Location = location,
                IPConfigurations = 
                {
                    new NetworkInterfaceIPConfigurationData()
                    {
                        Name = "Primary",
                        Primary = true,
                        Subnet = new SubnetData() { Id = vnet.GetSubnet(subnetName).Value.Id },
                        PrivateIPAllocationMethod = IPAllocationMethod.Dynamic,
                        PublicIPAddress = new PublicIPAddressData() { Id = ipAddress.Id }
                    }
                }
            };

            var nicJob = await resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync(WaitUntil.Completed, nicName, nicData);
            NetworkInterfaceResource nic = nicJob.Value;
            log.LogInformation("--------End creating Network Interface--------");

            
            // TODO: Availability Set

            // TODO: Network Security Group

            log.LogInformation("--------Start creating Virtual Machine--------");
            VirtualMachineData vmData = new VirtualMachineData(location)
            {
                NetworkProfile = new VirtualMachineNetworkProfile { NetworkInterfaces = { new VirtualMachineNetworkInterfaceReference() { Id = nic.Id } } },
                OSProfile = new VirtualMachineOSProfile
                {
                    ComputerName = vmName,
                    AdminUsername = adminUsername,
                    AdminPassword = adminPassword,
                    LinuxConfiguration = new LinuxConfiguration { DisablePasswordAuthentication = false, ProvisionVmAgent = true }
                },
                StorageProfile = new VirtualMachineStorageProfile()
                {
                    ImageReference = new ImageReference()
                    {
                        Offer = "0001-com-ubuntu-server-jammy",
                        Publisher = "canonical",
                        Sku = "22_04-lts-gen2",
                        Version = "latest"
                    },
                    OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                    {
                        ManagedDisk = new VirtualMachineManagedDisk() { StorageAccountType = StorageAccountType.StandardSsdLrs }
                    }
                },
                HardwareProfile = new VirtualMachineHardwareProfile() { VmSize = "Standard_B1ls" },
                //AvailabilitySet = new Azure.ResourceManager.Compute.Models.SubResource() { Id = availabilitySet.Id }
            };

            var vmJob = await vmCollection.CreateOrUpdateAsync(WaitUntil.Completed, vmName, vmData);
            VirtualMachineResource vm = vmJob.Value;
            log.LogInformation("--------End creating Virtual Machine--------");

            log.LogInformation("--------Start creating System Topic--------");
            string systemTopicName = String.Format("{0}-topic", expID);
            SystemTopicData systemTopicData = new SystemTopicData("global"){
                Source = resourceGroup.Id,
                TopicType = "microsoft.resources.resourcegroups"
            };
            var systemTopicJob = await resourceGroup.GetSystemTopics().CreateOrUpdateAsync(WaitUntil.Completed, systemTopicName, systemTopicData);
            SystemTopicResource systemTopic = systemTopicJob.Value;
            log.LogInformation("--------End creating System Topic--------");

            log.LogInformation("--------Start creating Event Grid Subscription--------");
            string eventSubscriptionName = String.Format("{0}-event-grid-subscription", vmName);
            TokenRequestContext trc = new TokenRequestContext(new string[] { "https://management.azure.com" }, tenantId: tenantId);
            CancellationToken ct = new CancellationToken();
            log.LogInformation($"Attempting to obtain Token for Service Principal using Tenant Id:{tenantId}");
            
            AccessToken accessToken = await credential.GetTokenAsync(trc, ct);
            string token = accessToken.Token;

            TokenCredentials tc = new TokenCredentials(token);
            
            //EventGridManagementClient eventGridMgmtClient = new EventGridManagementClient(tc)
            //{
            //    SubscriptionId = subscriptionID,
            //    LongRunningOperationRetryTimeout = 2
            //};

            string eventSubscriptionScope = systemTopic.Id;

            log.LogInformation($"Creating an event subscription to topic {systemTopicName}...");

            // EventGridSubscriptionData eventSubscriptionData = new EventGridSubscriptionData() 
            // {
            //     Destination = new WebHookEventSubscriptionDestination(),
            //     EventDeliverySchema = EventDeliverySchema.EventGridSchema,
            //     Filter = new EventSubscriptionFilter()
            //     {
            //         IncludedEventTypes = 
            //         {
            //             "Microsoft.Resources.ResourceActionSuccess",
            //             "Microsoft.Resources.ResourceDeleteSuccess",
            //             "Microsoft.Resources.ResourceWriteSuccess"
            //         }
            //     }
            // };

            //EventSubscription eventSubscriptionData = new EventSubscription() 
            //{
            //    Destination = new WebHookEventSubscriptionDestination(),
            //    EventDeliverySchema = EventDeliverySchema.EventGridSchema,
            //    Filter = new EventSubscriptionFilter()
            //    {
            //        IncludedEventTypes = new List<string>
            //        {
            //            "Microsoft.Resources.ResourceActionSuccess",
            //            "Microsoft.Resources.ResourceDeleteSuccess",
            //            "Microsoft.Resources.ResourceWriteSuccess"
            //        }
            //    }
            //};

  
            //var eventSubscriptionJob = await eventGridMgmtClient.SystemTopicEventSubscriptions.CreateOrUpdateAsync(eventSubscriptionScope, eventSubscriptionName, eventSubscriptionData);
            //EventSubscription eventSubscription = await eventGridMgmtClient.EventSubscriptions.CreateOrUpdateAsync(eventSubscriptionScope, eventSubscriptionName, eventSubscriptionData);//expID, systemTopicName, eventSubscriptionName, eventSubscriptionData);
            //EventSubscription createdEventSubscription = 
            log.LogInformation("--------End creating Event Grid Subscription--------");

            return vm;
        }

        private static async Task CreateVMLogic(ILogger log)
        {
            log.LogInformation("--------Start creating VM Logic--------");

            log.LogInformation("--------End creating VM Logic--------");
        }
    }
}
