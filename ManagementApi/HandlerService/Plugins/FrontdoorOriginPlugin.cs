using System.Collections;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Azure.ResourceManager.Cdn;
using Azure.ResourceManager.Cdn.Models;
using Azure.ResourceManager.Resources;
using ManagementApi.Cosmos.Wrapper;
using ManagementApi.Queue.Wrapper;
using ManagementApi.SharedModels;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Handler.Plugins;


public class FrontdoorOriginPlugin(string queue, ILogger logger) : PluginBase(queue, logger) {

    public override async Task HandleOneMessage() {

        (FrontdoorOrigin? parsedMessage, ServiceBusReceivedMessage? message) = 
            await QueueWrapper.ReceiveMessage<FrontdoorOrigin>(Queue, TimeSpan.FromMilliseconds(100));
        if (parsedMessage is not null) {
            Logger.LogInformation($"Received FrontdoorOrigin message: {parsedMessage}");
            await TryHandleRequest(parsedMessage);
            await QueueWrapper.CloseMessage(Queue, message!);
        }
        return;
    }

    public async Task TryHandleRequest(FrontdoorOrigin message) {
        try {
            // Update database
            await UpdateStatus(message.CorrelationId!, "In progress");
            try {
                System.Console.WriteLine($"Try add origin to cosmos");
                await CosmosWrapper.AddItem<FrontdoorOrigin>(CosmosContainersEnum.Requests, message, Logger);
            } catch (CosmosWrapperAlreadyExistsException) {
                System.Console.WriteLine($"Try patch origin in cosmos");
                await CosmosWrapper.ReplaceItem<FrontdoorOrigin>(CosmosContainersEnum.Requests, message, Logger);
            }

            // Create Origin
            await UpdateStatus(message.CorrelationId!, "Deploying");
            Logger.LogInformation($"Try create origin in frontdoor");
            await CreateFrontdoorOrigin(
                groupName: $"fd-og-{message.id!}",
                originName: $"fd-o-{message.id!}",
                hostName: message.Hostname!,
                httpPort: message.HttpPort!.Value,
                httpsPort: message.HttpsPort!.Value,
                pattern: $"/{message.id!}",
                routeName: $"fd-rt-{message.id!}"
            );
            await UpdateStatus(message.CorrelationId!, "Completed");

        } catch (Exception ex) {
            throw new Exception($"Corrupted message dropped: {message}: {ex.Message}");
        }
    }

    async Task CreateFrontdoorOrigin(string groupName, string originName, string hostName, int httpPort, 
                                     int httpsPort, string pattern, string routeName) { 
        
        (SubscriptionResource subscription, ResourceGroupResource resourceGroup) = await LoginArmClient();
        ProfileResource profile = await resourceGroup.GetProfileAsync("fdoor-profile-cosmos");
        FrontDoorOriginGroupResource originGroup = await CreateOriginGroup(profile, groupName);
        await CreateOrigin(originGroup, originName, hostName, httpPort, httpsPort);
        await CreateRoute(profile, originGroup, pattern, routeName);
    }

    /// <summary>
    /// Creates a Frontdoor origin group.
    /// </summary>
    /// <param name="profile">Front profile resource to place the origin group in.</param>
    /// <returns>Origin group resource</returns>
    async Task<FrontDoorOriginGroupResource> CreateOriginGroup(ProfileResource profile, string groupName) {

        FrontDoorOriginGroupCollection originGroupCollection = profile.GetFrontDoorOriginGroups();
        LoadBalancingSettings loadBalancingSettings = new LoadBalancingSettings { SampleSize=4, AdditionalLatencyInMilliseconds=50, SuccessfulSamplesRequired=3};
        FrontDoorOriginGroupData data = new FrontDoorOriginGroupData {LoadBalancingSettings = loadBalancingSettings};
        ArmOperation<FrontDoorOriginGroupResource> originGroup = await originGroupCollection.CreateOrUpdateAsync(
            Azure.WaitUntil.Completed, originGroupName: groupName, data: data);
        return originGroup.Value;
    }


    /// <summary>
    /// Creates a Frontdoor origin. 
    /// </summary>
    /// <param name="originGroup">Origin group to place new origin in.</param>
    /// <returns></returns>
    async Task CreateOrigin(FrontDoorOriginGroupResource originGroup, string originName, string hostName,
        int httpPort, int httpsPort) {

        FrontDoorOriginCollection frontDoorOrigins = originGroup.GetFrontDoorOrigins();
        FrontDoorOriginData originData = new() { 
            HostName=hostName,
            HttpPort=httpPort,
            HttpsPort=httpsPort,
            Priority=1,
            Weight=1000,
            EnabledState=EnabledState.Enabled,
        };
        await frontDoorOrigins.CreateOrUpdateAsync(Azure.WaitUntil.Completed, originName: originName, data: originData);
    }

    /// <summary>
    /// Create a Frontdoor route, linking an endpoint path to an origin group. 
    /// </summary>
    /// <param name="profile">Frontdoor profile to create route in</param>
    /// <param name="originGroup">Frontdoor origin group to link route to.</param>
    /// <returns></returns>
    async Task CreateRoute(ProfileResource profile, FrontDoorOriginGroupResource originGroup, string pattern,
                           string routeName) {

        FrontDoorEndpointResource endpoint = profile.GetFrontDoorEndpoint("cosmos");
        FrontDoorRouteCollection routes = endpoint.GetFrontDoorRoutes();
        FrontDoorRouteData routeData = new();
        routeData.SupportedProtocols.Add(FrontDoorEndpointProtocol.Http);
        routeData.SupportedProtocols.Add(FrontDoorEndpointProtocol.Https);
        routeData.OriginGroupId = originGroup.Id;
        routeData.LinkToDefaultDomain = LinkToDefaultDomain.Enabled;
        routeData.ForwardingProtocol = ForwardingProtocol.MatchRequest;
        routeData.PatternsToMatch.Add(pattern);
        await routes.CreateOrUpdateAsync(Azure.WaitUntil.Completed, routeName, routeData);
    }
}