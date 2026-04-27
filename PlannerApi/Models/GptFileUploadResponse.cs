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