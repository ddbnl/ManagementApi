using ApiService.Cosmos.Wrapper;
using ApiService.Cosmos.Wrapper.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApiService;


public static class WebApplicationExtensions {

    public static WebApplication MapGets(this WebApplication app) {

        app.MapGet("api/waf_rule", async (
            [FromServices] CosmosWrapper db,
            [FromQuery] string? id) => {

                ArgumentNullException.ThrowIfNull(id);

                return await db.GetItem<WafRule>(id);
            }
        )
        .WithName("GetWaf")
        .WithOpenApi(operation => {
            operation.Description = "Get a WAF rule";
            operation.Summary = "Get a WAF rule";
            return operation;
        })
        .Produces<WafRule>(StatusCodes.Status200OK);

        return app;
    }

    public static WebApplication MapPuts(this WebApplication app) {

        app.MapPut("api/waf_rule", async (
            [FromServices] CosmosWrapper db,
            [FromBody]
            [FromQuery] string? id, string? description, string? hostname, int? httpPort, int? httpsPort) => {
                ArgumentNullException.ThrowIfNull(id);
                ArgumentNullException.ThrowIfNull(description);
                ArgumentNullException.ThrowIfNull(hostname);
                ArgumentNullException.ThrowIfNull(httpPort);
                ArgumentNullException.ThrowIfNull(httpsPort);
                return await db.AddItem<WafRule>(
                    new WafRule(id) {Description=description, Hostname=hostname, HttpPort=httpPort, HttpsPort=httpsPort}
                );
            }
        )
        .WithName("PutWaf")
        .WithOpenApi(operation => {
            operation.Description = "Create a new WAF rule";
            operation.Summary = "Create a new WAF rule";
            return operation;
        })
        .Produces<WafRule>(StatusCodes.Status200OK);

        return app;
    }
}