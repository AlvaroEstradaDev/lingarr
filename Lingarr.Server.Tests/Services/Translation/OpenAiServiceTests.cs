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

public class OpenAiServiceTests
{
    private const string TemplateWithTemperature =
        "{\"model\":\"{model}\",\"temperature\":0.1,\"messages\":[" +
        "{\"role\":\"system\",\"content\":\"{systemPrompt}\"}," +
        "{\"role\":\"user\",\"content\":\"{userMessage}\"}]}";

    private static Mock<ISettingService> BuildSettingsMock(string template)
    {
        var settingsMock = new Mock<ISettingService>();
        var settings = new Dictionary<string, string>
        {
            { SettingKeys.Translation.OpenAi.Model, "gpt-4o" },
            { SettingKeys.Translation.OpenAi.RequestTemplate, template },
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
        settingsMock.Setup(s => s.GetEncryptedSetting(SettingKeys.Translation.OpenAi.ApiKey))
            .ReturnsAsync("sk-test");
        return settingsMock;
    }

    [Fact]
    public async Task TranslateBatchAsync_PreservesUserTemperature_AndInjectsResponseFormat()
    {
        var inner = "{\"translations\":[{\"position\":1,\"line\":\"hola\"}]}";
        var respJson = JsonSerializer.Serialize(new
        { choices = new[] { new { message = new { content = inner } } } });

        var settingsMock = BuildSettingsMock(TemplateWithTemperature);
        var body = "";
        var handler = HttpCapture.Handler(s => body = s, HttpStatusCode.OK, respJson);

        var svc = new OpenAiService(settingsMock.Object,
            NullLogger<OpenAiService>.Instance, new LanguageCodeService(),
            new RequestTemplateService(), new HttpClient(handler.Object));

        var batch = new List<BatchSubtitleItem> { new() { Position = 1, Line = "hello" } };
        await svc.TranslateBatchAsync(batch, "en", "es", CancellationToken.None);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(0.1, doc.RootElement.GetProperty("temperature").GetDouble());
        Assert.True(doc.RootElement.TryGetProperty("response_format", out _));
    }
}
