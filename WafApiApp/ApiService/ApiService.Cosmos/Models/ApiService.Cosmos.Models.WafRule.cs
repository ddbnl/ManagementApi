namespace ApiService.Cosmos.Wrapper.Models;

public class FrontdoorOrigin(string id) : ModelBase(id) {
    public string? Description { get; set; }
    public string? Hostname { get; set; }
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
}