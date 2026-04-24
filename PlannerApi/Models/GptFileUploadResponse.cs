using System.Text.Json.Serialization;

namespace PlannerApi.Models;

public sealed class GptFileUploadResponse
{
    public string? Id { get; set; }
    public string? Filename { get; set; }
    public bool Status { get; set; }
}

public sealed class GptPromptResponse
{
    public string? Command { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
}

public sealed class GptExtractedJobInfo
{
    [JsonPropertyName("Received Date & Time")]
    public string? ReceivedDateTime { get; set; }

    public string? Sender { get; set; }
    public string? To { get; set; }
    public string? Cc { get; set; }
    public string? Name { get; set; }
    public string? Agency { get; set; }
    public string? Title { get; set; }
    public string? Role { get; set; }
    public string? Headcount { get; set; }

    [JsonPropertyName("Job Details")]
    public string? JobDetails { get; set; }

    public string? Seniority { get; set; }

    [JsonPropertyName("Duration of Contract")]
    public string? DurationOfContract { get; set; }

    [JsonPropertyName("Selection Process")]
    public string? SelectionProcess { get; set; }

    [JsonPropertyName("Contact Details")]
    public string? ContactDetails { get; set; }

    [JsonPropertyName("Special Notes")]
    public string? SpecialNotes { get; set; }

    public string? Timeline { get; set; }

    [JsonPropertyName("Indicative Onboarding Timeline")]
    public string? IndicativeOnboardingTimeline { get; set; }

    public string? Deadline { get; set; }
}
