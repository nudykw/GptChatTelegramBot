using System.Text.Json.Serialization;

namespace ServiceLayer.Services.OpenAI.Models;

public class LiteLlmModelInfo
{
    [JsonPropertyName("input_cost_per_token")]
    public double? InputCostPerToken { get; set; }

    [JsonPropertyName("output_cost_per_token")]
    public double? OutputCostPerToken { get; set; }

    [JsonPropertyName("litellm_provider")]
    public string? Provider { get; set; }
}
