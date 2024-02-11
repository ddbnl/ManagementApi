namespace ManagementApi.SharedModels;

public class Status(string id) : ModelBase(id, type: "Status") {
    public string? StatusString { get; set; }
    public string? StartTime { get; set; }
}