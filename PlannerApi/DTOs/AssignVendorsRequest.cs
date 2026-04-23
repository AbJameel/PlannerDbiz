namespace PlannerApi.DTOs;

public class AssignVendorsRequest
{
    public List<int> VendorIds { get; set; } = [];
    public string AssignmentNote { get; set; } = string.Empty;
    public string UpdateStatusTo { get; set; } = "Assigned to Vendor";
}
