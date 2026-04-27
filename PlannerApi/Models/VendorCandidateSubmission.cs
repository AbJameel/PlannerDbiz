
namespace PlannerApi.Models;

public class VendorCandidateSubmission
{
    public int SubmissionId { get; set; }
    public int PlannerId { get; set; }
    public int VendorId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ContactDetail { get; set; } = string.Empty;
    public string VisaType { get; set; } = string.Empty;
    public string ResumeFile { get; set; } = string.Empty;
    public string DbizResumeFile { get; set; } = string.Empty;
    public string CandidateStatus { get; set; } = "Draft";
    public bool IsSubmitted { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
}
