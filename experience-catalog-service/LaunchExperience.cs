using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Network.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using Azure.ResourceManager.Compute.Models;
using Microsoft.Azure.Cosmos;
using System.Linq;

namespace Solipsist.ExperienceCatalog
{
    public static class LaunchExperience
    {
        static readonly string adminUsername = "jsipko";
        static readonly string adminPassword = "solipsist4ever!";

        [FunctionName("LaunchExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "exp/launch")] HttpRequest req,
            ILogger log)
        {
            string expID = req.Query["expid"];

            log.LogInformation($"LaunchExperience HTTP function triggered for id: {expID}");

            return await RunLocal(log, expID, adminUsername, adminPassword);
        }

        // TODO: This function should be atomic
        // TODO 2: These functions are doing too much and should be refactored
        public static async Task<IActionResult> RunLocal(ILogger log, string expID, string adminUsername, string adminPassword)
        {
            // Get connection strings
            string storageConnectionString = Utilities.GetBlobStorageConnectionString("ExperienceStorage");
            string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnectionString");

            // TODO: Check the running status of an experience
            // Connect to metadata db and query the experience metadata container
            using CosmosClient cosmosClient = new(connectionString: cosmosConnectionString);
            var metadataContainer = cosmosClient.GetContainer("experiences", "metadata");

            QueryDefinition queryDefinition = new QueryDefinition(
                "select * from metadata m where m.id = @expID")
                .WithParameter("@expID", expID);
            var resultSet = metadataContainer.GetItemQueryIterator<ExperienceMetadata>(queryDefinition).ToAsyncEnumerable();
            ExperienceMetadata experience = await resultSet.FirstAsync();
            if (experience == null) 
            { 
                return new NotFoundResult();
            }

            if (experience.state != ExperienceState.Stopped)
            {
                return new UnprocessableEntityResult();
            }

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
            var createRGJob = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, expID, new ResourceGroupData(AzureLocation.EastUS2));
            ResourceGroupResource resourceGroup = createRGJob.Value;
            log.LogInformation("--------End fetching Resource Group--------");

            // Create / start VM
            log.LogInformation("--------Start fetching Virtual Machine--------");
            VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
            VirtualMachineResource vmResource = null;
            if (vmCollection.Exists(experience.name))
            {
                vmResource = await vmCollection.GetAsync(experience.name);
            }
            else
            {
                vmResource = await CreateVirtualMachine(log, resourceGroup, vmCollection, experience.name, AzureLocation.EastUS2, adminUsername, adminPassword);
            }

            log.LogInformation("--------End fetching Virtual Machine--------");
            log.LogInformation("VM ID: " + vmResource.Id);

            // Start the VM and update the Experience state
            vmResource.PowerOn(WaitUntil.Started);
            experience.state = ExperienceState.Starting;
            await metadataContainer.UpsertItemAsync(experience);

            JsonResult vmResult = new JsonResult(vmResource);

            return new OkObjectResult(vmResult);
        }

        private static async Task<VirtualMachineResource> CreateVirtualMachine(ILogger log, ResourceGroupResource resourceGroup, VirtualMachineCollection vmCollection, string vmName, AzureLocation location, string adminUsername, string adminPassword)
        {
            log.LogInformation("--------Start creating IP Address--------");
            string ipAddressName = String.Format("{0}_ip", vmName);
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
            string vnetName = String.Format("{0}_vnet", vmName);
            string subnetName = String.Format("{0}_subnet", vmName);
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
            string nicName = String.Format("{0}_nic", vmName);
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

            return vm;
        }
    }
}
