using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceLayer.Services.GptChat.Models
{
    internal class GptUsage
    {
        [JsonInclude]
        [JsonPropertyName("prompt_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        internal int? PromptTokens { get; set; }

        [JsonInclude]
        [JsonPropertyName("completion_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        internal int? CompletionTokens { get; set; }

        [JsonInclude]
        [JsonPropertyName("total_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        internal int? TotalTokens { get; set; }

        internal GptUsage()
        {
        }

        internal GptUsage(int? promptTokens, int? completionTokens, int? totalTokens)
        {
            PromptTokens = promptTokens;
            CompletionTokens = completionTokens;
            TotalTokens = totalTokens;
        }
        public static implicit operator GptUsage(OpenAI.Usage usage)
        {
            if (usage == null)
            {
                throw new ArgumentNullException("Usage");
            }

            return new GptUsage(usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
        }
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
