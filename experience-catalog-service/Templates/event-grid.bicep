param subscriptionId string = subscription().id
param resourceGroupName string = resourceGroup().name
param experienceName string = 'Untitled'

resource systemTopic 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
  name: '${resourceGroupName}-topic'
  location: 'global'
  properties: {
    source: '${subscriptionId}/resourceGroups/${resourceGroupName}'
    topicType: 'microsoft.resources.resourcegroups'
  }
}

resource systemTopicVMEventSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2022-06-15' = {
  parent: systemTopic
  name: '${experienceName}-vm-event-subscription'
  properties: {
    destination: {
      properties: {
        maxEventsPerBatch: 1
        preferredBatchSizeInKilobytes: 64
      }
      endpointType: 'WebHook'
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Resources.ResourceActionSuccess'
        'Microsoft.Resources.ResourceDeleteSuccess'
        'Microsoft.Resources.ResourceWriteSuccess'
      ]
    }
    eventDeliverySchema: 'EventGridSchema'
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}