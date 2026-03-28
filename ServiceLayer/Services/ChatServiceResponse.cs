using System.Collections.Generic;

namespace ServiceLayer.Services;

public class ChatServiceResponse
{
    public required List<string> Choices { get; set; }
    public int? TotalTokens { get; set; }
    public required string ProviderName { get; set; }
    public required string ModelName { get; set; }
}
