using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using Newtonsoft.Json;

namespace GiftCompanion.Core.LLM
{
    public sealed class DialogueRequest
    {
        public string Message { get; }
        public InteractionSource Source { get; }
        public string SystemPrompt { get; }
        public IReadOnlyList<LLMMessage> History { get; }
        public DialogueRequest(string message, InteractionSource source, string systemPrompt = null, IReadOnlyList<LLMMessage> history = null)
        {
            Message = message;
            Source = source;
            SystemPrompt = systemPrompt;
            History = history ?? Array.Empty<LLMMessage>();
        }
    }

    public sealed class LLMMessage
    {
        public string Role { get; }
        public string Content { get; }
        public LLMMessage(string role, string content)
        {
            if (role != "user" && role != "assistant") throw new ArgumentException("Invalid history role.");
            Role = role; Content = content ?? throw new ArgumentNullException(nameof(content));
        }
    }
    public interface IMemoryLLMService
    {
        Task<string> CompleteJsonAsync(string instruction, string input, int maxTokens, CancellationToken ct);
    }

    public sealed class LLMChunk
    {
        public string Text { get; }
        public bool IsFinal { get; }
        public DialogueResult Result { get; }
        public DialoguePresentation Presentation { get; }
        public LLMChunk(string text) => Text = text;
        private LLMChunk(DialoguePresentation presentation) { Text = string.Empty; Presentation = presentation; }
        private LLMChunk(DialogueResult result) { Text = string.Empty; IsFinal = true; Result = result; }
        public static LLMChunk Complete(DialogueResult result) => new LLMChunk(result);
        public static LLMChunk Metadata(DialoguePresentation presentation) => new LLMChunk(presentation);
    }

    public interface ILLMService
    {
        bool IsConfigured { get; }
        IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request, CancellationToken ct);
    }

    public sealed class LLMServiceException : Exception
    {
        public string Code { get; }
        public LLMServiceException(string code) : base(code) => Code = code;
    }

    public sealed class LLMService : ILLMService, IMemoryLLMService, IDisposable
    {
        private readonly HttpClient http;
        private readonly DeepSeekApiClient client;
        public bool IsConfigured => client != null;
        public GiftCompanion.Core.Agent.IAgentModel AgentModel => client;
        public LLMService(string apiKey = null, string model = "deepseek-flash", int timeoutSeconds = 30, HttpMessageHandler handler = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return;
            http = handler == null ? new HttpClient() : new HttpClient(handler);
            http.Timeout = Timeout.InfiniteTimeSpan; // One transport timeout covers headers and SSE body.
            client = new DeepSeekApiClient(http, apiKey, model, timeoutSeconds);
        }

        public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(
            DialogueRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            if (!IsConfigured) throw new LLMServiceException("LLMNotConfigured");
            await using var iterator = client.StreamAsync(request.Message, ct, request.SystemPrompt, request.History).GetAsyncEnumerator(ct);
            while (true)
            {
                bool next;
                try { next = await iterator.MoveNextAsync(); }
                catch (Exception error) { throw new LLMServiceException(ErrorCode(error, ct)); }
                if (!next) yield break;
                yield return new LLMChunk(iterator.Current);
            }
        }
        public async Task<string> CompleteJsonAsync(string instruction, string input, int maxTokens, CancellationToken ct)
        {
            if (!IsConfigured) throw new LLMServiceException("LLMNotConfigured");
            try { return await client.CompleteJsonAsync(instruction, input, maxTokens, ct).ConfigureAwait(false); }
            catch (Exception error) { throw new LLMServiceException(ErrorCode(error, ct)); }
        }
        internal static string ErrorCode(Exception error, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return "Canceled";
            if (error is OperationCanceledException) return "Timeout";
            if (error is DeepSeekHttpException status)
                return status.Code == 401 ? "Unauthorized" : status.Code == 429 ? "RateLimited" : "HttpError";
            if (error is EndOfStreamException) return "StreamTruncated";
            if (error is JsonException || error is InvalidDataException) return "ProtocolError";
            if (error is HttpRequestException || error is IOException) return "NetworkError";
            return "LLMFailed";
        }
        public void Dispose() => http?.Dispose();
    }
}
