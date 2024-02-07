using System.Net;
using ApiService.Cosmos.Wrapper;
using ApiService.Cosmos.Wrapper.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiService;


public static class WebApplicationExtensions {

    public static WebApplication MapGets(this WebApplication app) {

        app.MapGet("api/frontdoor/origin", async (
            [FromServices] CosmosWrapper db,
            [FromQuery] string? id) => {

                ArgumentNullException.ThrowIfNull(id);

                try {
                    FrontdoorOrigin? wafRule = await db.GetItem<FrontdoorOrigin>(id);
                    return Results.Ok(wafRule);
                } catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
                    return Results.NotFound("frontdoor origin ID not found.");
                }
            }
        )
        .WithName("GetFrontdoorOrigin")
        .WithOpenApi(operation => {
            operation.Description = "Get a registered frontdoor origin";
            operation.Summary = "Get a registered frontdoor origin";
            return operation;
        })
        .RequireAuthorization(["user"])
        .Produces<FrontdoorOrigin>(StatusCodes.Status200OK);

        return app;
    }

    public static WebApplication MapPuts(this WebApplication app) {

        app.MapPut("api/frontdoor/origin", async (
            [FromServices] CosmosWrapper db,
            [FromBody]
            [FromQuery] string? id, string? description, string? hostname, int? httpPort, int? httpsPort) => {
                ArgumentNullException.ThrowIfNull(id);
                ArgumentNullException.ThrowIfNull(description);
                ArgumentNullException.ThrowIfNull(hostname);
                ArgumentNullException.ThrowIfNull(httpPort);
                ArgumentNullException.ThrowIfNull(httpsPort);
                return await db.AddItem<FrontdoorOrigin>(
                    new FrontdoorOrigin(id) {Description=description, Hostname=hostname, HttpPort=httpPort, HttpsPort=httpsPort}
                );
            }
        )
        .WithName("PutFrontdoorOrigin")
        .WithOpenApi(operation => {
            operation.Description = "Register a new frontdoor origin";
            operation.Summary = "Register a new frontdoor origin";
            return operation;
        })
        .RequireAuthorization(["user", "spn"])
        .Produces<FrontdoorOrigin>(StatusCodes.Status200OK);

        return app;
    }
}