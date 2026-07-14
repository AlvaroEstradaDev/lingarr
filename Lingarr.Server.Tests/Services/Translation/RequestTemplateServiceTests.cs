using System.Collections.Generic;
using System.Text.Json;
using Lingarr.Core.Configuration;
using Lingarr.Server.Services.Translation;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class RequestTemplateServiceTests
{
    private readonly RequestTemplateService _svc = new();

    [Fact]
    public void BuildRequestBody_WithNullExtraFields_ReturnsTemplateWithPlaceholdersReplaced()
    {
        var template = "{\"model\":\"{model}\",\"messages\":[{\"role\":\"system\",\"content\":\"{systemPrompt}\"}]}";

        var result = _svc.BuildRequestBody(template, new Dictionary<string, string>
        {
            ["model"] = "llama",
            ["systemPrompt"] = "translate"
        });

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("llama", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void BuildRequestBody_AddsExtraField_WhenKeyAbsent()
    {
        var template = "{\"model\":\"{model}\"}";

        var result = _svc.BuildRequestBody(template,
            new Dictionary<string, string> { ["model"] = "llama" },
            new Dictionary<string, object?> { ["temperature"] = 0.3 });

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(0.3, doc.RootElement.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public void BuildRequestBody_PreservesTemplateValue_WhenKeyAlreadyPresent()
    {
        var template = "{\"model\":\"{model}\",\"temperature\":0.9}";

        var result = _svc.BuildRequestBody(template,
            new Dictionary<string, string> { ["model"] = "llama" },
            new Dictionary<string, object?> { ["temperature"] = 0.1 });

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(0.9, doc.RootElement.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public void BuildRequestBody_AddsComplexExtraField_WhenKeyAbsent()
    {
        var template = "{\"model\":\"{model}\"}";
        var schema = new { type = "json_schema", json_schema = new { name = "x" } };

        var result = _svc.BuildRequestBody(template,
            new Dictionary<string, string> { ["model"] = "llama" },
            new Dictionary<string, object?> { ["response_format"] = schema });

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("json_schema", doc.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public void GetDefaultTemplate_LocalAiChat_HasQwenThinkingDefaults()
    {
        var json = _svc.GetDefaultTemplate(SettingKeys.Translation.LocalAi.ChatRequestTemplate);
        Assert.NotNull(json);

        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;
        Assert.Equal(4096, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(1024, root.GetProperty("reasoning_budget_tokens").GetInt32());
        var kwargs = root.GetProperty("chat_template_kwargs");
        Assert.True(kwargs.GetProperty("enable_thinking").GetBoolean());
        Assert.Equal(1024, kwargs.GetProperty("thinking_budget").GetInt32());
    }

    [Fact]
    public void BuildRequestBody_PreservesChatTemplateKwargs_WhenPresentInTemplate()
    {
        var template =
            "{\"model\":\"{model}\",\"chat_template_kwargs\":{\"enable_thinking\":false}}";

        var result = _svc.BuildRequestBody(template,
            new Dictionary<string, string> { ["model"] = "llama" },
            new Dictionary<string, object?> { ["response_format"] = new { type = "json_schema" } });

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());
        Assert.Equal("json_schema", doc.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }
}
