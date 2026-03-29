using System.Collections.Generic;

namespace ServiceLayer.Services;

public class ChatServiceResponse
{
    public required List<string> Choices { get; set; }
    public int? TotalTokens { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? ReasoningTokens { get; set; }
    public decimal? Cost { get; set; }
    public double? LatencySeconds { get; set; }
    public double? TokensPerSecond { get; set; }
    public required string ProviderName { get; set; }
    public required string ModelName { get; set; }
}
