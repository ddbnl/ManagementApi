using System.Net;
using Microsoft.AspNetCore.Mvc;
using ManagementApi.Cosmos.Wrapper;
using ManagementApi.Queue.Wrapper;
using ManagementApi.SharedModels;

namespace ManagementApi.ApiService.Routers;

public class FrontdoorRouter(ILogger<FrontdoorRouter> logger): RouterBase(logger, urlFragment: "frontdoor") {

    protected async Task<IResult> GetOrigin(string? id) {
        Logger.LogInformation($"Getting frontdoor origin ID: {id}");
        ArgumentNullException.ThrowIfNull(id);

        try {
            FrontdoorOrigin? dbResult = await CosmosWrapper.GetItem<FrontdoorOrigin>(CosmosContainersEnum.Requests,
                id, partitionKey: "FrontdoorOrigin");
            return Results.Ok(dbResult);
        } catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
            return Results.NotFound("frontdoor origin ID not found.");
        }
    }

    protected async Task<IResult> PutOrigin(string? id, string? description, string? hostname, int? httpPort,
            int? httpsPort) {
                
        Logger.LogInformation($"Putting frontdoor origin ID: {id}");
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(hostname);
        ArgumentNullException.ThrowIfNull(httpPort);
        ArgumentNullException.ThrowIfNull(httpsPort);
        if (id.Contains(' ')) {
            logger.LogError("Invalid ID: whitespace detected");
            return Results.BadRequest("ID cannot contain whitespace");
        }

        string correlationId = Guid.NewGuid().ToString();
        await QueueWrapper.SendMessage(
            new FrontdoorOrigin(id, correlationId) 
                { Description=description, Hostname=hostname, HttpPort=httpPort, HttpsPort=httpsPort },
                queue: "frontdoor-origin"
        );

        await CosmosWrapper.AddItem<Status>(
            CosmosContainersEnum.Requests,
            new Status(correlationId) { StatusString="Queued", StartTime=DateTime.UtcNow.ToString() },
            logger
        );

        return Results.Ok($"Registered frontdoor origin. Correlation ID: {correlationId}.");
    }

    protected async Task<IResult> DeleteOrigin(string? id) {

        logger.LogInformation($"Deleting frontdoor origin ID: {id}");
        ArgumentNullException.ThrowIfNull(id);

        string correlationId = Guid.NewGuid().ToString();
        await QueueWrapper.SendMessage(new FrontdoorOrigin(id, correlationId), queue: "frontdoor-origin");
        await CosmosWrapper.AddItem<Status>(
            CosmosContainersEnum.Requests,
            new Status(correlationId) { StatusString="Queued", StartTime=DateTime.UtcNow.ToString() },
            logger
        );
        return Results.Ok($"Unregistering frontdoor origin. Correlation ID: {correlationId}.");
    }

    public override void AddRoutes(WebApplication app) {

    // Get /frontdoor/origin
    app.MapGet($"{UrlFragment}/origin", async (
        [FromQuery] string? id)
        => await GetOrigin(id))
        .WithName("GetFrontdoorOrigin")
        .WithOpenApi(operation => {
            operation.Description = "Get a registered frontdoor origin";
            operation.Summary = "Get a registered frontdoor origin";
            return operation;
        })
        .RequireAuthorization(["user"])
        .Produces<FrontdoorOrigin>(StatusCodes.Status200OK);

    // Put /frontdoor/origin
    app.MapPut($"{UrlFragment}/origin", async (
        [FromBody]
        [FromQuery] string? id, string? description, string? hostname, int? httpPort, int? httpsPort)
         => await PutOrigin(id, description, hostname, httpPort, httpsPort))
        .WithName("PutFrontdoorOrigin")
        .WithOpenApi(operation => {
            operation.Description = "Register a new frontdoor origin";
            operation.Summary = "Register a new frontdoor origin";
            return operation;
        })
        .RequireAuthorization(new[] { "user"})
        .Produces<FrontdoorOrigin>(StatusCodes.Status200OK);

    // Delete /frontdoor/origin
    app.MapDelete($"{UrlFragment}/origin", async (
        [FromBody]
        [FromQuery] string? id)
         => await DeleteOrigin(id))
        .WithName("DeleteFrontdoorOrigin")
        .WithOpenApi(operation => {
            operation.Description = "Unregister a new frontdoor origin";
            operation.Summary = "Unregister a new frontdoor origin";
            return operation;
        })
        .RequireAuthorization(new[] { "user"})
        .Produces<FrontdoorOrigin>(StatusCodes.Status200OK);
        }
}
