using Microsoft.Azure.Cosmos;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Logging.ApplicationInsights;
using ManagementApi.Cosmos.Wrapper;
using ManagementApi.Queue.Wrapper;
using ManagementApi.ApiService.Routers;
using Azure.Identity;


// Get Cosmos secret
string cosmosSecret = Environment.GetEnvironmentVariable("cosmos")!;
string appInsightsSecret = Environment.GetEnvironmentVariable("appinsights")!;

// Initialize database
CosmosDatabaseConfig cosmosDatabaseConfig = new("ApiDatabase", throughput: 400);
CosmosContainerConfig containerConfig = new("Requests", throughput: 400, partitionKeyPath: "/type", 
    indexingMode: IndexingMode.Consistent, automatic: true, includedPath: new IncludedPath { Path = "/*" });
CosmosWrapper.Database = cosmosDatabaseConfig;
CosmosWrapper.CosmosContainers.Add(CosmosContainersEnum.Requests, containerConfig);

// Initialize queue
QueueWrapper.Connect();

// Initialize app
var builder = WebApplication.CreateBuilder(args);

//  Initialize logging
DefaultAzureCredential credential = new DefaultAzureCredential();
builder.Logging.AddApplicationInsights(
        configureTelemetryConfiguration: (config) => {
            config.ConnectionString = appInsightsSecret;
            config.SetAzureTokenCredential(credential);
        },
        configureApplicationInsightsLoggerOptions: (options) => { }
);
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>(typeof(Program).FullName, LogLevel.Warning);  

//  Initialize services
builder.Services.AddScoped<RouterBase, FrontdoorRouter>();
builder.Services.AddScoped<RouterBase, StatusRouter>();

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user", policy => policy.RequireRole("user"));
    options.AddPolicy("spn", policy => policy.RequireRole("spn"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ApiService", Version = "v1" });
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Description = "Microsoft Entra login.",
        Name = "oauth2.0",
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows {
            AuthorizationCode = new OpenApiOAuthFlow {
                AuthorizationUrl = new Uri(builder.Configuration["SwaggerAzureAD:AuthorizationUrl"]!),
                TokenUrl = new Uri(builder.Configuration["SwaggerAzureAD:TokenUrl"]!),
                Scopes = new Dictionary<string, string> {
                    {builder.Configuration["SwaggerAzureAd:Scope"]!, "Access API as user"}
                }
            }
        }
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement() {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference{Type=ReferenceType.SecurityScheme, Id="oauth2"}
            },
            new[] {builder.Configuration["SwaggerAzureAd:Scope"]! }
        
        }});
});

builder.Services.AddCors();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.OAuthClientId(builder.Configuration["SwaggerAzureAd:ClientId"]!);
    c.OAuthUsePkce();
    c.OAuthScopeSeparator(" ");
});
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();


using (var scope = app.Services.CreateScope())  {
    var services = scope.ServiceProvider.GetServices<RouterBase>();
    foreach (var item in services) {
        item.AddRoutes(app);
    }
    app.Run();
}
