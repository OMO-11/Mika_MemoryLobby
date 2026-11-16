using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.LLM;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class DeepSeekApiTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            public string Body;
            public HttpStatusCode Status = HttpStatusCode.OK;
            public string RequestBody;
            public int Requests;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Requests++;
                Assert.That(request.RequestUri, Is.EqualTo(DeepSeekApiClient.Endpoint));
                Assert.That(request.Headers.Authorization.Scheme, Is.EqualTo("Bearer"));
                RequestBody = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "text/event-stream") };
            }
        }

        [Test]
        public async Task StreamingHandlesHeartbeatRoleUnicodeAndDone()
        {
            var handler = new FakeHandler { Body = ": keepalive\n\ndata: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"。\"}}]}\n\ndata: [DONE]\n\n" };
            using var http = new HttpClient(handler);
            var values = new List<string>();
            await foreach (var delta in new DeepSeekApiClient(http, "fake-key").StreamAsync("test")) values.Add(delta);
            Assert.That(string.Concat(values), Is.EqualTo("你好。"));
            Assert.That(handler.RequestBody, Does.Contain("deepseek-flash"));
            Assert.That(handler.RequestBody, Does.Not.Contain("fake-key"));
        }

        [Test]
        public void HttpErrorsDoNotExposeResponseSecrets()
        {
            var handler = new FakeHandler { Body = "private-response", Status = HttpStatusCode.Unauthorized };
            using var http = new HttpClient(handler);
            var error = Assert.ThrowsAsync<DeepSeekHttpException>(async () =>
            {
                await foreach (var unused in new DeepSeekApiClient(http, "fake-key").StreamAsync("test")) { }
            });
            Assert.That(error.Message, Does.Contain("401"));
            Assert.That(error.Message, Does.Not.Contain("private-response").And.Not.Contain("fake-key"));
        }

        [Test]
        public void TruncatedStreamIsNotReportedAsSuccess()
        {
            using var http = new HttpClient(new FakeHandler { Body = "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n" });
            Assert.ThrowsAsync<System.IO.EndOfStreamException>(async () =>
            {
                await foreach (var unused in new DeepSeekApiClient(http, "fake-key").StreamAsync("test")) { }
            });
        }

        [Test]
        public void CanceledRequestDoesNotSend()
        {
            var handler = new FakeHandler { Body = "data: [DONE]\n\n" };
            using var http = new HttpClient(handler);
            using var ct = new CancellationTokenSource();
            ct.Cancel();
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () =>
            {
                await foreach (var unused in new DeepSeekApiClient(http, "fake-key").StreamAsync("test", ct.Token)) { }
            });
            Assert.That(handler.Requests, Is.EqualTo(0));
        }
    }
}
