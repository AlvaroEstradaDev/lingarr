using System.Text.Json.Serialization;

namespace Lingarr.Server.Models.RequestTemplates;

public class LocalAiChatTemplate
{
    [JsonPropertyName("model")] 
    public string Model { get; set; } = "{model}";

    [JsonPropertyName("max_tokens")] 
    public int MaxTokens { get; set; } = 4096;

    [JsonPropertyName("reasoning_budget_tokens")] 
    public int ReasoningBudgetTokens { get; set; } = 1024;

    [JsonPropertyName("chat_template_kwargs")] 
    public ChatTemplateKwargs ChatTemplateKwargs { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } =
    [
        new()
        {
            Role = "system", 
            Content = "{systemPrompt}"
        },
        new() { 
            Role = "user", 
            Content = "{userMessage}" 
        }
    ];
}

public class ChatTemplateKwargs
{
    [JsonPropertyName("enable_thinking")] 
    public bool EnableThinking { get; set; } = true;

    [JsonPropertyName("thinking_budget")] 
    public int ThinkingBudget { get; set; } = 1024;
}
