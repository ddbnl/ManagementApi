using ApiService.Cosmos.Wrapper.Models;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace ApiService.Cosmos.Wrapper;

public class CosmosWrapper {

    private static string? EndpointUri;
    private static string? PrimaryKey;
    private static CosmosDatabaseConfig? Database;
    private static CosmosContainerConfig? Container;

    public CosmosWrapper WithEndpointUri(string endpointUri) {
        EndpointUri = endpointUri;
        return this;
    }

    public CosmosWrapper WithPrimaryKey(string primaryKey) {
        PrimaryKey = primaryKey;
        return this;
    }

    private CosmosClient CreateClient() {
        return new(accountEndpoint: EndpointUri, authKeyOrResourceToken: PrimaryKey);
    }

    public async Task<CosmosWrapper> CreateDatabase(CosmosDatabaseConfig cosmosDatabase) {

        using CosmosClient client = CreateClient();
        await _CreateDatabase(cosmosDatabase, client);
        return this;
    }

    private async Task<DatabaseResponse> _CreateDatabase(CosmosDatabaseConfig cosmosDatabase, CosmosClient client) {

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

    public async Task<CosmosWrapper> CreateContainer(CosmosContainerConfig container, CosmosDatabaseConfig database) {

        using CosmosClient client = CreateClient();
        await _CreateContainer(container, database, client);
        return this;
    }

    private async Task<ContainerResponse> _CreateContainer(
        CosmosContainerConfig container, CosmosDatabaseConfig database, CosmosClient client) {

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
        System.Console.WriteLine($"Container: {container.Id}, status: {status}");  // TODO: Logging
        Container = container;
        return containerResponse;
    }

    public async Task<T> GetItem<T> (string id) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, Container!.Id);

        T item = await container.ReadItemAsync<T>(id: id, partitionKey: new PartitionKey(id));
        return item;
    }

    public async Task<CosmosWrapper> AddItem<T> (T item) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, Container!.Id);

        try {
            System.Console.WriteLine($"AA {item.id}");
            ItemResponse<T> itemExistsResponse = await container.ReadItemAsync<T>(
                id: item.id, partitionKey: new PartitionKey(item.id));
        } catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            System.Console.WriteLine($"BB {item.id}");
            ItemResponse<T> createItemResponse = await container.CreateItemAsync(item, partitionKey: new(item.id));
            System.Console.WriteLine($"Created item: {item.id} in database {Database!.Id} and container {Container!.Id} already exists"); // TODO LOG
        };
        return this;
    }

    public async Task<CosmosWrapper> RemoveItem<T> (T item) where T : ModelBase {

        using CosmosClient client = CreateClient();
        Container container = client.GetContainer(Database!.Id, Container!.Id);
        try {
            ItemResponse<T> itemExistsResponse = await container.ReadItemAsync<T>(
                id: item.id, partitionKey: new PartitionKey(item.id));
        } catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            System.Console.WriteLine($"Already non-existent: Item: {item.id} in database {Database!.Id} and container {Container!.Id}"); // TODO LOG
        } finally {
            ItemResponse<T> removeItemResponse = await container.DeleteItemAsync<T>(item.id, partitionKey: new(item.id));
            System.Console.WriteLine($"Removed item: {item.id} in database {Database!.Id} and container {Container!.Id} already exists"); // TODO LOG
        };
        return this;
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

