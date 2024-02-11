namespace ManagementApi.SharedModels;

public class ModelBase(string id, string type) {
    public string id {get; set; } = id;
    public string type { get; set; } = type;
}