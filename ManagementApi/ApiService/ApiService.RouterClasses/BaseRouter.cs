namespace ManagementApi.ApiService.Routers;

public abstract class RouterBase(ILogger<RouterBase> logger, string urlFragment) {
    public string UrlFragment = urlFragment;
    protected ILogger Logger = logger;
    public abstract void AddRoutes(WebApplication app);
}