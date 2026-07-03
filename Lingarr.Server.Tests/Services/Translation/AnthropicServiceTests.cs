using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lingarr.Core.Configuration;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Models.Batch;
using Lingarr.Server.Services;
using Lingarr.Server.Services.Translation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class AnthropicServiceTests
{
    private const string TemplateWithMaxTokens =
        "{\"model\":\"{model}\",\"max_tokens\":4096,\"system\":\"{systemPrompt}\"," +
        "\"messages\":[{\"role\":\"user\",\"content\":\"{userMessage}\"}]}";

    private static Mock<ISettingService> BuildSettingsMock(string template)
    {
        var settingsMock = new Mock<ISettingService>();
        var settings = new Dictionary<string, string>
        {
            { SettingKeys.Translation.Anthropic.Model, "claude-3-5-sonnet" },
            { SettingKeys.Translation.Anthropic.Version, "2023-06-01" },
            { SettingKeys.Translation.Anthropic.RequestTemplate, template },
            { SettingKeys.Translation.AiPrompt, "translate" },
            { SettingKeys.Translation.AiContextPrompt, "" },
            { SettingKeys.Translation.AiContextPromptEnabled, "false" },
            { SettingKeys.Translation.RequestTimeout, "5" },
            { SettingKeys.Translation.MaxRetries, "1" },
            { SettingKeys.Translation.RetryDelay, "1" },
            { SettingKeys.Translation.RetryDelayMultiplier, "1" },
            { SettingKeys.Translation.LanguageCodeFormat, "false" }
        };
        settingsMock.Setup(s => s.GetSettings(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(settings);
        settingsMock.Setup(s => s.GetEncryptedSetting(SettingKeys.Translation.Anthropic.ApiKey))
            .ReturnsAsync("sk-ant-test");
        return settingsMock;
    }

    [Fact]
    public async Task TranslateBatchAsync_PreservesUserMaxTokens_AndInjectsTools()
    {
        var resp = "{\"stop_reason\":\"tool_use\",\"content\":[{\"type\":\"tool_use\"," +
                   "\"input\":{\"translations\":[{\"position\":1,\"line\":\"hola\"}]}}]}";

        var settingsMock = BuildSettingsMock(TemplateWithMaxTokens);
        var body = "";
        var handler = HttpCapture.Handler(s => body = s, HttpStatusCode.OK, resp);

        var svc = new AnthropicService(settingsMock.Object, new HttpClient(handler.Object),
            NullLogger<AnthropicService>.Instance, new LanguageCodeService(),
            new RequestTemplateService());

        var batch = new List<BatchSubtitleItem> { new() { Position = 1, Line = "hello" } };
        await svc.TranslateBatchAsync(batch, "en", "es", CancellationToken.None);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(4096, doc.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("tools", out _));
        Assert.True(doc.RootElement.TryGetProperty("tool_choice", out _));
    }
}
