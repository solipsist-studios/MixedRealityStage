param ownerId string
param experienceId string
param experienceName string
param virtualNetworkName string = '${experienceName}-vnet'
param networkSecurityGroupName string = '${experienceName}-sg'
param subnetName string = '${experienceName}-subnet'
param location string = 'eastus2'
param vmSize string = 'Standard_B1ls'
param adminUsername string = 'solipsistadmin'
param storageAccountName string = 'solipsistexperiencecatal'
param storageResourceGroupName string = 'solipsistexperiencecatal'

@secure()
param adminPassword string = 'solipsist4ever!'

// Corresponds to the Storage Blob Data Contributor role
// @secure()
// param roleDefinitionId string = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

var subnetRef = '${virtualNetwork.id}/subnets/${subnetName}'

resource publicIPAddress 'Microsoft.Network/publicIPAddresses@2022-01-01' = {
  name: '${experienceName}-ip'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Regional'
  }
  properties: {
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Dynamic'
    idleTimeoutInMinutes: 4
    ipTags: []
  }
}

resource networkInterface 'Microsoft.Network/networkInterfaces@2022-01-01' = {
  name: '${experienceName}-nic'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'Primary'
        type: 'Microsoft.Network/networkInterfaces/ipConfigurations'
        properties: {
          privateIPAddress: '10.0.0.4'
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: publicIPAddress.id
          }
          subnet: {
            id: subnetRef
          }
          primary: true
          privateIPAddressVersion: 'IPv4'
        }
      }
    ]
    dnsSettings: {
      dnsServers: []
    }
    enableAcceleratedNetworking: false
    enableIPForwarding: false
    networkSecurityGroup: {
      id: networkSecurityGroup.id
    }
    nicType: 'Standard'
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2022-01-01' = {
  name: virtualNetworkName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: '10.0.0.0/24'
          delegations: []
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
        }
        type: 'Microsoft.Network/virtualNetworks/subnets'
      }
    ]
    virtualNetworkPeerings: []
    enableDdosProtection: false
  }
}

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2022-01-01' = {
  name: networkSecurityGroupName
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowUnity7777Inbound'
        type: 'Microsoft.Network/networkSecurityGroups/securityRules'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '7777'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 110
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
    ]
  }
}

resource virtualMachine 'Microsoft.Compute/virtualMachines@2022-03-01' = {
  name: experienceName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    storageProfile: {
      imageReference: {
        publisher: 'canonical'
        offer: '0001-com-ubuntu-server-jammy'
        sku: '22_04-lts-gen2'
        version: 'latest'
      }
      osDisk: {
        osType: 'Linux'
        name: '${experienceName}_OsDisk_1_3ac54c711c7342b7969f0c876e48d82f'
        createOption: 'FromImage'
        caching: 'ReadWrite'
        managedDisk: {
          storageAccountType: 'StandardSSD_LRS'
        }
        deleteOption: 'Detach'
        diskSizeGB: 30
      }
      dataDisks: []
    }
    osProfile: {
      computerName: experienceName
      adminUsername: adminUsername
      adminPassword: adminPassword
      linuxConfiguration: {
        disablePasswordAuthentication: false
        provisionVMAgent: true
        patchSettings: {
          patchMode: 'ImageDefault'
          assessmentMode: 'ImageDefault'
        }
      }
      secrets: []
      allowExtensionOperations: true
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: networkInterface.id
        }
      ]
    }
  }
}

output principalId string = virtualMachine.identity.principalId

module storageRoleAssignment './storage-role-assignment.bicep' = {
  name: 'storageRoleAssignment'
  scope: resourceGroup(storageResourceGroupName)
  params: {
    ownerId: ownerId
    storageAccountName: storageAccountName
    principalId: virtualMachine.identity.principalId
    //roleDefinitionId: roleDefinitionId
  }
}

resource virtualMachineBootstrapScript 'Microsoft.Compute/virtualMachines/extensions@2019-03-01' = {
  parent: virtualMachine
  name: 'provisionserver'
  location: location
  properties: {
    publisher: 'Microsoft.Azure.Extensions'
    type: 'CustomScript'
    typeHandlerVersion: '2.1'
    protectedSettings: {
      fileUris: [
        'https://solipsistexperiencecatal.blob.core.windows.net/${ownerId}/${experienceId}'
        'https://solipsistexperiencecatal.blob.core.windows.net/experiences/launch-unity.sh'
      ]
      commandToExecute: '.\\launch-unity.sh ${experienceId}'
      managedIdentity : {}
    }
  }
}
