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

public class LocalAiServiceTests
{
    private static Mock<ISettingService> BuildSettingsMock(string chatTemplate, string genTemplate, string endpoint)
    {
        var settingsMock = new Mock<ISettingService>();
        var settings = new Dictionary<string, string>
        {
            { SettingKeys.Translation.LocalAi.Model, "llama3" },
            { SettingKeys.Translation.LocalAi.Endpoint, endpoint },
            { SettingKeys.Translation.LocalAi.ChatRequestTemplate, chatTemplate },
            { SettingKeys.Translation.LocalAi.GenerateRequestTemplate, genTemplate },
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
        settingsMock.Setup(s => s.GetEncryptedSetting(It.IsAny<string>()))
            .ReturnsAsync("");
        return settingsMock;
    }

    private const string ChatTemplateWithKwargs =
        "{\"model\":\"{model}\",\"messages\":[{\"role\":\"system\",\"content\":\"{systemPrompt}\"}," +
        "{\"role\":\"user\",\"content\":\"{userMessage}\"}],\"chat_template_kwargs\":{\"enable_thinking\":false}}";

    [Fact]
    public async Task TranslateBatchAsync_StructuredPath_SendsUserChatTemplateKwargsAndInjectsResponseFormat()
    {
        var inner = "{\"translations\":[{\"position\":1,\"line\":\"hola\"}]}";
        var respJson = JsonSerializer.Serialize(new
        { choices = new[] { new { message = new { content = inner } } } });

        var settingsMock = BuildSettingsMock(ChatTemplateWithKwargs, "",
            "http://x/v1/chat/completions");
        var body = "";
        var handler = HttpCapture.Handler(s => body = s, HttpStatusCode.OK, respJson);

        var svc = new LocalAiService(settingsMock.Object, new HttpClient(handler.Object),
            NullLogger<LocalAiService>.Instance, new LanguageCodeService(), new RequestTemplateService());

        var batch = new List<BatchSubtitleItem> { new() { Position = 1, Line = "hello" } };
        await svc.TranslateBatchAsync(batch, "en", "es", CancellationToken.None);

        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());
        Assert.True(doc.RootElement.TryGetProperty("response_format", out _));
    }

    [Fact]
    public async Task TranslateBatchAsync_GeneratePath_SendsGenerateTemplateAndUserBatchContent()
    {
        var inner = "[{\"position\":1,\"line\":\"hola\"}]";
        var respJson = JsonSerializer.Serialize(new { response = inner });

        // endpoint NOT ending in "completions" -> generate path
        var settingsMock = BuildSettingsMock("", "", "http://x/v1/completions-extra");
        var body = "";
        var handler = HttpCapture.Handler(s => body = s, HttpStatusCode.OK, respJson);

        var svc = new LocalAiService(settingsMock.Object, new HttpClient(handler.Object),
            NullLogger<LocalAiService>.Instance, new LanguageCodeService(), new RequestTemplateService());

        var batch = new List<BatchSubtitleItem> { new() { Position = 1, Line = "hello" } };
        await svc.TranslateBatchAsync(batch, "en", "es", CancellationToken.None);

        using var doc = JsonDocument.Parse(body);
        // default generate template puts systemPrompt + userMessage into "prompt"
        var prompt = doc.RootElement.GetProperty("prompt").GetString()!;
        Assert.Contains("translate", prompt);
        Assert.Contains("Please return the response as a JSON array", prompt);
        Assert.Contains("\"Position\":1", prompt); // serialized batch present
    }
}
