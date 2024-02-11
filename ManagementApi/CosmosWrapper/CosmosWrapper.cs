using Azure.Identity;
using ManagementApi.SharedModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ManagementApi.Cosmos.Wrapper;

public static class CosmosWrapper {

    public static CosmosDatabaseConfig? Database;
    public static Dictionary<CosmosContainersEnum, CosmosContainerConfig> CosmosContainers = [];

    private static CosmosClient CreateClient() {
        CosmosClient client =  new (
            accountEndpoint: Environment.GetEnvironmentVariable("cosmos"), new DefaultAzureCredential()
        );
        System.Console.WriteLine("Connected to Cosmos DB");
        return client;
    }

    public static async Task CreateDatabase(CosmosDatabaseConfig cosmosDatabase) {

        using CosmosClient client = CreateClient();
        await _CreateDatabase(cosmosDatabase, client);
    }

    private static async Task<DatabaseResponse> _CreateDatabase(CosmosDatabaseConfig cosmosDatabase, CosmosClient client) {

        DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(
            id: cosmosDatabase.Id, throughput: cosmosDatabase.Throughput);

        string status = databaseResponse.StatusCode switch
        {
            HttpStatusCode.OK => "Exists",
            HttpStatusCode.Created => "Created",
            _ => "Unknown"
        };

        Database = cosmosDatabase;
        System.Console.WriteLine($"Database: {cosmosDatabase.Id}, status: {status}");  // TODO: Logging
        return databaseResponse;
    }    

    public static async Task CreateContainer(CosmosContainersEnum id, CosmosContainerConfig container,
        CosmosDatabaseConfig database, ILogger logger) {

        using CosmosClient client = CreateClient();
        await _CreateContainer(id, container, database, client, logger);
    }

    private static async Task<ContainerResponse> _CreateContainer(
        CosmosContainersEnum id, CosmosContainerConfig container, CosmosDatabaseConfig database, CosmosClient client,
        ILogger logger) {

        DatabaseResponse databaseResponse = await _CreateDatabase(database, client);
        IndexingPolicy indexingPolicy = new() {
            IndexingMode = container.IndexingMode,
            Automatic = container.Automatic,
            IncludedPaths = { container.IncludedPath }
        };

        ContainerProperties containerProperties = new(id: container.Id, partitionKeyPath: container.PartitionKeyPath) {
            IndexingPolicy = indexingPolicy
        };
        
        ContainerResponse containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
            containerProperties, throughput: container.Throughput);

        string status = databaseResponse.StatusCode switch {
            HttpStatusCode.OK => "exists",
            HttpStatusCode.Created => "Created",
            _ => "Unknown"
        };
        logger.LogDebug($"Container: {container.Id}, status: {status}");  // TODO: Logging
        CosmosContainers.Add(id, container);
        return containerResponse;
    }

    public static async Task<T> GetItem<T> (CosmosContainersEnum containerId, string id, string partitionKey) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, CosmosContainers[containerId].Id);

        T item = await container.ReadItemAsync<T>(id: id, partitionKey: new PartitionKey(partitionKey));
        return item;
    }

    public static async Task PatchItem<T> (CosmosContainersEnum containerId, string id, string partitionKey,
        List<PatchOperation> patchOperations, ILogger logger) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, CosmosContainers[containerId].Id);

        try {
            ItemResponse<T> itemExistsResponse = await container.ReadItemAsync<T>(
                id: id, partitionKey: new PartitionKey(partitionKey));
        } catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            logger.LogError($"Item: {id} in database {Database!.Id} and container {containerId} does not exist"); // TODO LOG
        };
        
        ItemResponse<T> patchItemResponse = await container.PatchItemAsync<T>(id, partitionKey: new(partitionKey), patchOperations);
    }

    public static async Task AddItem<T> (CosmosContainersEnum containerId, T item, ILogger logger) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, CosmosContainers[containerId].Id);

        try {
            ItemResponse<T> itemExistsResponse = await container.ReadItemAsync<T>(
                id: item.id, partitionKey: new PartitionKey(item.type));
            throw new CosmosWrapperAlreadyExistsException();
        } catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            ItemResponse<T> createItemResponse = await container.CreateItemAsync(item, partitionKey: new(item.type));
            logger.LogInformation($"Created item: {item.id} in database {Database!.Id} and container {containerId}"); // TODO LOG
            return;
        }
    }

    public static async Task ReplaceItem<T> (CosmosContainersEnum containerId, T item, ILogger logger) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, CosmosContainers[containerId].Id);

        ItemResponse<T> replaceItemResponse = await container.ReplaceItemAsync<T>(item, item.id, new PartitionKey(item.type));
    }
    public static async Task RemoveItem<T>(CosmosContainersEnum containerId, string id, string partitionKey,
            ILogger logger) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, CosmosContainers[containerId].Id);
        try {
            ItemResponse<T> itemExistsResponse = await container.ReadItemAsync<T>(
                id: id, partitionKey: new PartitionKey(partitionKey));
        } catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            logger.LogInformation($"Already non-existent: Item: {id} in database {Database!.Id} and container {containerId}"); // TODO LOG
        } finally {
            ItemResponse<T> removeItemResponse = await container.DeleteItemAsync<T>(id, partitionKey: new(partitionKey));
            logger.LogInformation($"Removed item: {id} in database {Database!.Id} and container {containerId} already exists"); // TODO LOG
        };
    }
}


public class CosmosDatabaseConfig(string id, int throughput) {
    public string Id = id;
    public int Throughput = throughput;
}

public class CosmosContainerConfig(string id, int throughput, string partitionKeyPath, IndexingMode indexingMode,
    bool automatic, IncludedPath includedPath) {
    public string Id = id;
    public int Throughput = throughput;
    public string PartitionKeyPath = partitionKeyPath;
    public IndexingMode IndexingMode = indexingMode;
    public bool Automatic = automatic;
    public IncludedPath IncludedPath = includedPath;
}


public class CosmosWrapperAlreadyExistsException : Exception;