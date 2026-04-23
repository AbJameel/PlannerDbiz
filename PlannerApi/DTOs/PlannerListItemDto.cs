namespace PlannerApi.DTOs;

public class PlannerListItemDto
{
    public int Id { get; set; }
    public string PlannerNo { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string RequirementTitle { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "SGD";
    public DateTime SlaDate { get; set; }
    public int OpenPositions { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public DateTime ReceivedOn { get; set; }
}

public class PlannerListResponse
{
    public List<PlannerListItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

