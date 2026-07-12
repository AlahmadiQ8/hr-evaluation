// Azure Monitor "app is down" alerts for Taqyeem.
//
// Applied *after* `aspire deploy` (see .github/scripts/setup-alerts.sh) because the availability
// test needs the runtime web ingress FQDN and the Application Insights resource id, both of which
// only exist once the app is deployed. Re-applying is safe: `az deployment group create` upserts by
// resource name, so this module is idempotent.
//
// Creates, per environment:
//   * an email action group,
//   * a Standard availability web test that pings the public web app's /health from several regions,
//   * a Sev-1 metric alert when the availability test fails from multiple locations,
//   * a Sev-2 metric alert when the web Container App returns too many HTTP 5xx responses.

targetScope = 'resourceGroup'

@description('Azure region for the availability web test. Metric alerts and the action group are always global.')
param location string = resourceGroup().location

@description('Short environment name, e.g. "staging" or "production". Used in resource names.')
param environmentName string

@description('Resource ID of the Application Insights component the availability test links to.')
param appInsightsId string

@description('Public FQDN of the web Container App (no scheme), e.g. "web.happysea-1a2b3c4d.centralus.azurecontainerapps.io".')
param webAppFqdn string

@description('Resource ID of the web Container App, used for the HTTP 5xx metric alert.')
param webContainerAppId string

@description('Email address that receives the alerts.')
param notificationEmail string = 'momohammad@microsoft.com'

@description('Relative path probed by the availability test.')
param healthPath string = '/health'

@description('Number of failed test locations that triggers the availability alert.')
@minValue(1)
@maxValue(5)
param availabilityFailedLocationCount int = 2

@description('HTTP 5xx responses (Total) over the 5-minute window that triggers the server-error alert.')
@minValue(1)
param fivexxThreshold int = 10

@description('Azure availability-test location IDs to run the ping from (global coverage).')
param testLocationIds array = [
  'us-il-ch1-azr' // Central US
  'us-va-ash-azr' // East US
  'us-tx-sn1-azr' // South Central US
  'us-ca-sjc-azr' // West US
  'emea-nl-ams-azr' // West Europe
]

var webTestName = 'webtest-taqyeem-web-${environmentName}'
var actionGroupName = 'ag-taqyeem-${environmentName}'
// Action group short name is capped at 12 characters by Azure.
var actionGroupShortName = take('tqym${take(environmentName, 8)}', 12)

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: 'global'
  properties: {
    groupShortName: actionGroupShortName
    enabled: true
    emailReceivers: [
      {
        name: 'team-email'
        emailAddress: notificationEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource webTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: webTestName
  location: location
  // Links the web test to the Application Insights component (required by Azure).
  tags: {
    'hidden-link:${appInsightsId}': 'Resource'
  }
  kind: 'standard'
  properties: {
    SyntheticMonitorId: webTestName
    Name: 'Taqyeem web ${healthPath} (${environmentName})'
    Description: 'Pings the public Taqyeem web app health endpoint from multiple regions.'
    Enabled: true
    Frequency: 300
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [
      for id in testLocationIds: {
        Id: id
      }
    ]
    Request: {
      RequestUrl: 'https://${webAppFqdn}${healthPath}'
      HttpVerb: 'GET'
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: false
    }
  }
}

resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-availability-${environmentName}'
  location: 'global'
  properties: {
    description: 'Taqyeem web app is unreachable from ${availabilityFailedLocationCount}+ regions (${environmentName}).'
    severity: 1
    enabled: true
    scopes: [
      webTest.id
      appInsightsId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: webTest.id
      componentId: appInsightsId
      failedLocationCount: availabilityFailedLocationCount
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource serverErrorAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-web-5xx-${environmentName}'
  location: 'global'
  properties: {
    description: 'Taqyeem web app returned more than ${fivexxThreshold} HTTP 5xx responses in 5 minutes (${environmentName}).'
    severity: 2
    enabled: true
    scopes: [
      webContainerAppId
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xx'
          metricNamespace: 'microsoft.app/containerapps'
          metricName: 'Requests'
          dimensions: [
            {
              name: 'statusCodeCategory'
              operator: 'Include'
              values: [
                '5xx'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: fivexxThreshold
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

output actionGroupId string = actionGroup.id
output webTestId string = webTest.id
output availabilityAlertId string = availabilityAlert.id
output serverErrorAlertId string = serverErrorAlert.id
