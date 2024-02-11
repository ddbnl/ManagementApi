using System.Net;
using Microsoft.AspNetCore.Mvc;
using ManagementApi.Cosmos.Wrapper;
using ManagementApi.SharedModels;

namespace ManagementApi.ApiService.Routers;
public class StatusRouter(ILogger<StatusRouter> logger): RouterBase(logger, urlFragment: "status") {

    protected async Task<IResult> Get(string? id) {
        Logger.LogInformation($"Getting correlation ID: {id}");
            ArgumentNullException.ThrowIfNull(id);

            try {
                Status? dbResult = await CosmosWrapper.GetItem<Status>(CosmosContainersEnum.Requests, id, partitionKey: "Status");
                return Results.Ok(dbResult);
            } catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
                return Results.NotFound("CorrelationId not found.");
            }
    }

    public override void AddRoutes(WebApplication app) {

        app.MapGet($"{UrlFragment}", async (
            [FromQuery] string? id)
                => await Get(id))
            .WithName("GetStatus")
            .WithOpenApi(operation => {
                operation.Description = "Get the status of a registered job.";
                operation.Summary = "Get the status of a registered job.";
                return operation;
            })
            .RequireAuthorization(["user"])
            .Produces<Status>(StatusCodes.Status200OK);
    }
}