namespace ManagementApi.SharedModels;

public class FrontdoorOrigin(string id, string? correlationId) : ModelBase(id, type: "FrontdoorOrigin") {
    public string? Description { get; set; }
    public string? Hostname { get; set; }
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public string? CorrelationId {get; set; } = correlationId;
}