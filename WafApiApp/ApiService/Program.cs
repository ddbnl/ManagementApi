using ApiService;
using ApiService.Cosmos.Wrapper;
using Microsoft.Azure.Cosmos;


// Initialize database
CosmosDatabaseConfig cosmosDatabaseConfig = new("ApiDatabase", throughput: 400);
CosmosContainerConfig cosmosContainerConfig = new("WafRules", throughput: 400, partitionKeyPath: "/id", 
    indexingMode: IndexingMode.Consistent, automatic: true, includedPath: new IncludedPath { Path = "/*" });

CosmosWrapper cosmosWrapper = await new CosmosWrapper()
    .WithEndpointUri("https://ddb-cosmos.documents.azure.com:443/")
    .WithPrimaryKey("RpHBhoM4c9u3t2AeIVKqhijb2dmfrUYmAWBVizcWLDn9Puz3kcLyytM3lcplnMqA0tKNMahkcRWbACDbFkqY4A==")
    .CreateContainer(cosmosContainerConfig, cosmosDatabaseConfig);

// Initialize app
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(new CosmosWrapper());

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.MapGets();
app.MapPuts();
app.Run();
