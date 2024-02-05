param frontdoorName string = ''
param appOrigins array = []


resource Frontdoor 'Microsoft.Cdn/profiles@2023-07-01-preview' existing = {
  name: frontdoorName
}

resource FrontdoorOriginGroup 'Microsoft.Cdn/profiles/originGroups@2023-07-01-preview' = [for appOrigin in appOrigins: {
  name: 'fd-${appOrigin.appId}-og'
  parent: Frontdoor
  properties: {
    healthProbeSettings: {
      probeIntervalInSeconds: 100
      probePath: '/'
      probeProtocol: 'Http'
      probeRequestType: 'HEAD'
    }
    loadBalancingSettings: {
      additionalLatencyInMilliseconds: 50
      sampleSize: 4
      successfulSamplesRequired: 3
    }
    sessionAffinityState: 'Disabled'
  }
}]


resource FrontdoorOriginGroupOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-07-01-preview' = [for (appOrigin, i) in appOrigins: {
  name: 'fd-${appOrigin.appId}-og-o'
  parent: FrontdoorOriginGroup[i]
  properties: {
    enabledState: 'Enabled'
    enforceCertificateNameCheck: false
    hostName: appOrigin.hostname
    httpPort: appOrigin.httpPort
    httpsPort: appOrigin.httpsPort
    priority: 3
    weight: 1000
  }
}]

resource FrontdoorEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2023-07-01-preview' existing = {
  parent: Frontdoor
  name: 'cosmos'
}

resource FrontdoorRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2023-07-01-preview' = [for (appOrigin, i) in appOrigins: {
  name: 'fd-${appOrigin.appId}-route'
  parent: FrontdoorEndpoint
  properties: {
    enabledState: 'Enabled'
    forwardingProtocol: 'MatchRequest'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    originGroup: {
      id: FrontdoorOriginGroup[i].id
    }
    patternsToMatch: [
      '/${appOrigin.appId}'
    ]
    supportedProtocols: [
      'Https'
    ]
  }
}]
