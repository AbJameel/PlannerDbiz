namespace PlannerApi.DTOs;

public class CreateTaskRequest
{
    public string Subject { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
