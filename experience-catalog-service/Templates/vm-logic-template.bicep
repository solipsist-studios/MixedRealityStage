param eventGridConnectionName string = 'azureeventgrid'
param eventGridPublishConnectionName string = 'azureeventgridpublish'
param experienceName string = 'Untitled'
param logicAppName string = '${experienceName}-logic'

param subscriptionId string = subscription().id
param resourceGroupName string = resourceGroup().name
param location string = resourceGroup().location

@secure()
param applicationSecret string = 'Q4-8Q~aXzcrziUSF1hrXIC_Y0Fr2v7NiheAXZcm_'

resource eventGridConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: eventGridConnectionName
  location: location
  properties: {
    displayName: 'event-grid-connection'
    nonSecretParameterValues: {
      'token:clientId': '83955fd1-3b4b-44d8-973d-7e03c3c82d0d'
      'token:TenantId': 'd5f06f52-0502-420b-8324-b77ca4aa68dd'
      'token:grantType': 'client_credentials'
    }
    parameterValues: {
      'token:clientSecret': applicationSecret
    }
    api: {
      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azureeventgrid')
    }
  }
}

resource eventGridPublishConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: eventGridPublishConnectionName
  location: location
  properties: {
    displayName: 'event-grid-publish-connection'
    parameterValues: {
      endpoint: 'https://experience-catalog-launch.eastus-1.eventgrid.azure.net/api/events'
    }
    api: {
      id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azureeventgridpublish')
    }
  }
}

resource logicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    state: 'Enabled'
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {
        '$connections': {
          defaultValue: {
          }
          type: 'Object'
        }
      }
      triggers: {
        When_a_resource_event_occurs: {
          splitOn: '@triggerBody()'
          type: 'ApiConnectionWebhook'
          inputs: {
            body: {
              properties: {
                destination: {
                  endpointType: 'webhook'
                  properties: {
                    endpointUrl: '@{listCallbackUrl()}'
                  }
                }
                filter: {
                  includedEventTypes: [
                    'Microsoft.Resources.ResourceActionSuccess'
                    'Microsoft.Resources.ResourceDeleteSuccess'
                    'Microsoft.Resources.ResourceWriteSuccess'
                  ]
                }
                topic: '/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}'
              }
            }
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'azureeventgrid\'][\'connectionId\']'
              }
            }
            path: '/subscriptions/@{encodeURIComponent(\'${subscriptionId}\')}/providers/@{encodeURIComponent(\'Microsoft.Resources.ResourceGroups\')}/resource/eventSubscriptions'
            queries: {
              subscriptionName: '${experienceName}-vm-event-subscription'
              'x-ms-api-version': '2017-09-15-preview'
            }
          }
        }
      }
      actions: {
        Condition: {
          actions: {
            Publish_Event: {
              runAfter: {
              }
              type: 'ApiConnection'
              inputs: {
                body: [
                  {
                    data: '@triggerBody()?[\'data\'][\'resourceUri\']'
                    eventType: 'vm-started'
                    id: '@{guid()}'
                    subject: 'experience-catalog'
                  }
                ]
                host: {
                  connection: {
                    name: '@parameters(\'$connections\')[\'azureeventgridpublish\'][\'connectionId\']'
                  }
                }
                method: 'post'
                path: '/eventGrid/api/events'
              }
            }
          }
          runAfter: {
          }
          expression: {
            and: [
              {
                equals: [
                  '@triggerBody()?[\'data\'][\'operationName\']'
                  'Microsoft.Compute/virtualMachines/start/action'
                ]
              }
            ]
          }
          type: 'If'
        }
      }
      outputs: {
      }
    }
    parameters: {
      '$connections': {
        value: {
          azureeventgrid: {
            connectionId: eventGridConnection.id
            connectionName: 'azureeventgrid'
            id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azureeventgrid')
          }
          azureeventgridpublish: {
            connectionId: eventGridPublishConnection.id
            connectionName: 'azureeventgridpublish'
            id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azureeventgridpublish')
          }
        }
      }
    }
  }
}
