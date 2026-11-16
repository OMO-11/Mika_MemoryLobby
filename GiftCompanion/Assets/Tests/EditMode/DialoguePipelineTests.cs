using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class DialoguePipelineTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public string Body = "data: {\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}\n\ndata: [DONE]\n\n";
            public HttpStatusCode Status = HttpStatusCode.OK;
            public bool Block;
            public string Request;
            public TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Request = await request.Content.ReadAsStringAsync();
                Started.TrySetResult(true);
                if (Block) await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "text/event-stream") };
            }
        }
        private static CharacterCore Core(ILLMService service) => new CharacterCore(new CharacterIdentity("test", "Test", "confirmed-persona", "confirmed-relationship"),
            new CharacterState(), new TestDatabase(), service, new MemoryService());

        [Test] public async Task SharedCoreStreamsTextAndCommitsOnlyCompleteReply()
        {
            var handler = new Handler();
            using var service = new LLMService("fake-key", handler: handler);
            var core = Core(service);
            var chunks = new List<LLMChunk>();
            await foreach (var chunk in core.StreamMessageAsync("hello", InteractionSource.Companion, default))
                if (chunk.Presentation == null) chunks.Add(chunk);
            Assert.That(chunks.Count, Is.EqualTo(2));
            Assert.That(chunks[0].Text, Is.EqualTo("你好"));
            Assert.That(chunks[1].Result.Success, Is.True);
            Assert.That(core.State.LastInteractionAt, Is.Not.Null);
            Assert.That(handler.Request, Does.Contain("confirmed-persona").And.Contain("confirmed-relationship").And.Not.Contain("fake-key"));
            Assert.That(core.State.Mood, Is.EqualTo(CharacterEmotion.Neutral));
            Assert.That(core.State.CurrentTopic, Is.Empty);
        }

        [TestCase(401, "Unauthorized")]
        [TestCase(429, "RateLimited")]
        [TestCase(503, "HttpError")]
        public async Task HttpFailureMapsToSafeCodeWithoutCommittingState(int status, string code)
        {
            using var service = new LLMService("fake-key", handler: new Handler { Status = (HttpStatusCode)status, Body = "private-body" });
            var core = Core(service);
            var result = await core.SendMessageAsync("hello", InteractionSource.AgentPet, default);
            Assert.That(result.ErrorCode, Is.EqualTo(code));
            Assert.That(result.Text, Is.Empty);
            Assert.That(core.State.LastInteractionAt, Is.Null);
        }

        [Test] public async Task TruncatedPartialReplyIsNotSuccess()
        {
            using var service = new LLMService("fake-key", handler: new Handler { Body = "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n" });
            var core = Core(service);
            var result = await core.SendMessageAsync("hello", InteractionSource.Companion, default);
            Assert.That(result.ErrorCode, Is.EqualTo("StreamTruncated"));
            Assert.That(result.Text, Is.Empty);
            Assert.That(core.State.LastInteractionAt, Is.Null);
        }

        [Test] public async Task PauseCancelsActiveRequestRejectsDuplicatesAndAllowsNextRequest()
        {
            var handler = new Handler { Block = true };
            using var service = new LLMService("fake-key", handler: handler);
            var core = Core(service);
            var first = core.SendMessageAsync("first", InteractionSource.Companion, default);
            await handler.Started.Task;
            Assert.That((await core.SendMessageAsync("duplicate", InteractionSource.Companion, default)).ErrorCode, Is.EqualTo("Busy"));
            await core.PauseAndCancelAsync(default);
            Assert.That((await first).ErrorCode, Is.EqualTo("Canceled"));
            Assert.That(core.State.LastInteractionAt, Is.Null);
            Assert.That((await core.SendMessageAsync("paused", InteractionSource.AgentPet, default)).ErrorCode, Is.EqualTo("Unavailable"));
            core.ResumeRequests(); handler.Block = false;
            Assert.That((await core.SendMessageAsync("next", InteractionSource.AgentPet, default)).Success, Is.True);
        }

        [Test] public async Task ConfiguredTimeoutIsDistinctFromCallerCancellation()
        {
            using var service = new LLMService("fake-key", timeoutSeconds: 1, handler: new Handler { Block = true });
            var core = Core(service);
            Assert.That((await core.SendMessageAsync("timeout", InteractionSource.Companion, default)).ErrorCode, Is.EqualTo("Timeout"));
            using var canceled = new CancellationTokenSource();
            var pending = core.SendMessageAsync("cancel", InteractionSource.Companion, canceled.Token);
            canceled.Cancel();
            Assert.That((await pending).ErrorCode, Is.EqualTo("Canceled"));
            Assert.That(core.State.LastInteractionAt, Is.Null);
        }
    }
}
