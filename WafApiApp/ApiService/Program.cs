using ApiService;
using ApiService.Cosmos.Wrapper;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;



// Get Cosmos secret
string kvUri = "https://kv-ddb-cosmos.vault.azure.net";
SecretClient client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
KeyVaultSecret secret = await client.GetSecretAsync("cosmos");

// Initialize database
CosmosDatabaseConfig cosmosDatabaseConfig = new("ApiDatabase", throughput: 400);
CosmosContainerConfig cosmosContainerConfig = new("WafRules", throughput: 400, partitionKeyPath: "/id", 
    indexingMode: IndexingMode.Consistent, automatic: true, includedPath: new IncludedPath { Path = "/*" });

CosmosWrapper cosmosWrapper = await new CosmosWrapper()
    .WithEndpointUri("https://ddb-cosmos.documents.azure.com:443/")
    .WithPrimaryKey(secret.Value)
    .CreateContainer(cosmosContainerConfig, cosmosDatabaseConfig);

// Initialize app
var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton(new CosmosWrapper());
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
app.MapGets();
app.MapPuts();
app.Run();
