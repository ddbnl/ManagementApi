using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using ManagementApi.Cosmos.Wrapper;
using ManagementApi.Queue.Wrapper;
using ManagementApi.SharedModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Handler.Plugins;


public abstract class PluginBase(string queue, ILogger logger) {
    public string Queue = queue;
    protected ILogger Logger = logger;

    public async Task UpdateStatus(string CorrelationId, string statusString) {
        Logger.LogDebug($"Updating status for {CorrelationId}: {statusString}");
        List<PatchOperation> patchOperations = new List<PatchOperation>() {
            PatchOperation.Replace("/StatusString", statusString)
        };
        await CosmosWrapper.PatchItem<Status>(CosmosContainersEnum.Requests, CorrelationId, "Status", patchOperations, Logger);
        Logger.LogInformation($"Updated status for {CorrelationId}: {statusString}");
    }

    public async Task<(SubscriptionResource, ResourceGroupResource)> LoginArmClient() {

        string subscriptionId = Environment.GetEnvironmentVariable("azureSubscription")!;
        string resourceGroupName = Environment.GetEnvironmentVariable("azureResourceGroup")!;
        Logger.LogDebug($"Logging into azure {subscriptionId}, {resourceGroupName}");
        ArmClient client = new ArmClient(new DefaultAzureCredential());
        SubscriptionCollection subscriptions = client.GetSubscriptions();
        SubscriptionResource subscription = subscriptions.Get(subscriptionId);
        ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
        ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(resourceGroupName);
        Logger.LogDebug($"Logged into azure.");
        return (subscription, resourceGroup);
    }

    public abstract Task HandleOneMessage();
}