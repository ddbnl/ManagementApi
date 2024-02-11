using ManagementApi.Cosmos.Wrapper;
using ManagementApi.Handler;
using ManagementApi.Handler.Plugins;
using ManagementApi.Queue.Wrapper;
using Microsoft.Azure.Cosmos;
using Azure.Identity;


using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


using InMemoryChannel channel = new InMemoryChannel();
try {
    ILogger logger = InitializeLogger();

    // Initialize database
    CosmosDatabaseConfig cosmosDatabaseConfig = new("ApiDatabase", throughput: 400);
    CosmosContainerConfig containerConfig = new("Requests", throughput: 400, partitionKeyPath: "/type", 
        indexingMode: IndexingMode.Consistent, automatic: true, includedPath: new IncludedPath { Path = "/*" });
    CosmosWrapper.Database = cosmosDatabaseConfig;
    CosmosWrapper.CosmosContainers.Add(CosmosContainersEnum.Requests, containerConfig);

    // Initialize queue
    QueueWrapper.Connect();

    Handler handler = new(logger);
    handler.AddPlugin(new FrontdoorOriginPlugin("frontdoor-origin", logger));
    await handler.RunLoop();
} finally {
    channel.Flush();
    await Task.Delay(TimeSpan.FromMilliseconds(1000));
}


ILogger InitializeLogger() {

    string appInsightsSecret = Environment.GetEnvironmentVariable("appinsights")!;
    DefaultAzureCredential credential = new DefaultAzureCredential();

    IServiceCollection services = new ServiceCollection();
    services.Configure<TelemetryConfiguration>(config => config.TelemetryChannel = channel);
    services.AddLogging(builder =>
    {
        // Only Application Insights is registered as a logger provider
        builder.AddApplicationInsights(
            configureTelemetryConfiguration: (config) => {
                config.ConnectionString = appInsightsSecret;
                config.SetAzureTokenCredential(credential);
            },
            configureApplicationInsightsLoggerOptions: (options) => { }
        );
    });

    IServiceProvider serviceProvider = services.BuildServiceProvider();
    ILogger logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    return logger;
}
