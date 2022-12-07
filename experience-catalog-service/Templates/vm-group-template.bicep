targetScope = 'subscription'

param ownerId string
param experienceId string
param experienceName string = 'Untitled'
param location string = 'eastus2'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-01-01' = {
  name: experienceId
  location: location
}

// Set up VM resource in module
module virtualMachineResource './vm-template.bicep' = {
  name: 'virtualMachineResourceDeployment'
  scope: resourceGroup
  params: {
    ownerId: ownerId
    experienceName: experienceName
    experienceId: experienceId
    location: location
  }
}

// module virtualMachineLogic './vm-logic-template.bicep' = {
//   name: 'virtualMachineLogicDeployment'
//   scope: resourceGroup
//   params: {
//     experienceName: experienceName
//     location: location
//   }
// }

// // Set up event grid in module
// module eventGrid './event-grid.bicep' = {
//   name: 'eventGridDeployment'
//   scope: resourceGroup
//   params: {
//     experienceName: experienceName
//   }
// }
