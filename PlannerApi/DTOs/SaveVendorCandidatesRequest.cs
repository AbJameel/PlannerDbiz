
using PlannerApi.Models;
namespace PlannerApi.DTOs;
public class SaveVendorCandidatesRequest
{
    public string VendorComment { get; set; } = string.Empty;
    public List<VendorCandidateSubmission> Items { get; set; } = [];
    public bool Submit { get; set; }
}
