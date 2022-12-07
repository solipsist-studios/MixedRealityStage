targetScope = 'resourceGroup'

param ownerId string
param principalId string

// Storage Blob Data Contributor role
param ownerRoleDefinitionId string = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Storage Blob Data Reader role
param experiencesRoleDefinitionId string = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'

param storageAccountName string = 'solipsistexperiencecatal'

// Get a symbolic reference to the experience catalog
resource experienceCatalogStorage 'Microsoft.Storage/storageAccounts@2021-02-01' existing = {
  name: storageAccountName
}

resource bs 'Microsoft.Storage/storageAccounts/blobServices@2021-08-01' existing = {
  name: 'default'
  parent: experienceCatalogStorage
}

resource experiencesStorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-08-01' existing = {
  name: 'experiences'
  parent: bs
}

var experiencesRoleAssignmentName = guid(principalId, experiencesRoleDefinitionId, resourceGroup().id)
resource experiencesRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: experiencesRoleAssignmentName
  scope: experiencesStorageContainer
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', experiencesRoleDefinitionId)
    principalId: principalId
  }
}

resource ownerStorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-08-01' existing = {
  name: ownerId
  parent: bs
}

var ownerRoleAssignmentName = guid(principalId, ownerRoleDefinitionId, resourceGroup().id)
resource ownerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: ownerRoleAssignmentName
  scope: ownerStorageContainer
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', ownerRoleDefinitionId)
    principalId: principalId
  }
}
