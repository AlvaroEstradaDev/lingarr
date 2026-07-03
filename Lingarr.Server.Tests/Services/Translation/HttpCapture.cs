using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;

namespace Lingarr.Server.Tests.Services.Translation;

internal static class HttpCapture
{
    internal static Mock<HttpMessageHandler> Handler(Action<string> capture, HttpStatusCode status, string respJson)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                var t = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                if (t != null) capture(t);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(respJson, Encoding.UTF8, "application/json")
            });
        return handler;
    }
}
