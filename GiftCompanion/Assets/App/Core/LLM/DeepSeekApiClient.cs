using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.LLM
{
    public sealed class DeepSeekHttpException : HttpRequestException
    {
        public int Code { get; }
        public DeepSeekHttpException(int code) : base($"DeepSeek HTTP {code}.") => Code = code;
    }

    // Transport only. Character identity and request lifecycle are owned by the shared services.
    public sealed partial class DeepSeekApiClient : GiftCompanion.Core.Agent.IAgentModel
    {
        private readonly HttpClient http;
        private readonly string apiKey;
        private readonly string model;
        private readonly int timeoutSeconds;
        public static readonly Uri Endpoint = new Uri("https://api.deepseek.com/chat/completions");

        public DeepSeekApiClient(HttpClient http, string apiKey, string model = "deepseek-flash", int timeoutSeconds = 30)
        {
            this.http = http ?? throw new ArgumentNullException(nameof(http));
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("DeepSeek API Key is missing.", nameof(apiKey));
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("DeepSeek model is missing.", nameof(model));
            this.apiKey = apiKey;
            this.model = model;
            if (timeoutSeconds < 1 || timeoutSeconds > 120) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            this.timeoutSeconds = timeoutSeconds;
        }

        public async IAsyncEnumerable<string> StreamAsync(string message,
            [EnumeratorCancellation] CancellationToken ct = default, string systemPrompt = null, IReadOnlyList<LLMMessage> history = null)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is empty.", nameof(message));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var token = timeout.Token;
            token.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            var messages = new JArray();
            if (!string.IsNullOrWhiteSpace(systemPrompt)) messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });
            if (history != null) foreach (var item in history)
                messages.Add(new JObject { ["role"] = item.Role, ["content"] = item.Content });
            messages.Add(new JObject { ["role"] = "user", ["content"] = message });
            var body = new JObject
            {
                ["model"] = model,
                ["stream"] = true,
                ["thinking"] = new JObject { ["type"] = "disabled" },
                ["max_tokens"] = 512,
                ["messages"] = messages
            };
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new DeepSeekHttpException((int)response.StatusCode); // Never echo request headers or response body.
            if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
                throw new InvalidDataException("DeepSeek did not return an SSE stream.");
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var data = new StringBuilder();
            using var canceled = token.Register(() => stream.Dispose());
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string line;
                try { line = await reader.ReadLineAsync().ConfigureAwait(false); }
                catch (Exception error) when (token.IsCancellationRequested &&
                    (error is IOException || error is ObjectDisposedException))
                { throw new OperationCanceledException(token); }
                token.ThrowIfCancellationRequested();
                if (line == null) throw new EndOfStreamException("DeepSeek stream ended before DONE.");
                if (line.Length != 0)
                {
                    if (line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        if (data.Length > 0) data.Append('\n');
                        data.Append(line.Substring(5).TrimStart(' '));
                        if (data.Length > 1024 * 1024) throw new InvalidDataException("SSE event is too large.");
                    }
                    continue; // Ignore heartbeat/comments and other SSE fields.
                }
                if (data.Length == 0) continue;
                string payload = data.ToString();
                data.Clear();
                if (payload.Trim() == "[DONE]") yield break;
                var chunk = JObject.Parse(payload);
                if (chunk["error"] != null) throw new InvalidDataException("DeepSeek returned a stream error.");
                string finish = chunk["choices"]?.First?["finish_reason"]?.Value<string>();
                if (!string.IsNullOrEmpty(finish) && finish != "stop") throw new EndOfStreamException("DeepSeek response did not finish normally.");
                string delta = chunk["choices"]?.First?["delta"]?["content"]?.Value<string>();
                if (!string.IsNullOrEmpty(delta)) yield return delta;
            }
        }
        public async Task<string> CompleteJsonAsync(string instruction, string input, int maxTokens, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(input) || maxTokens < 64 || maxTokens > 8192)
                throw new ArgumentException("Invalid JSON completion request.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var token = timeout.Token;
            token.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var body = new JObject {
                ["model"]=model, ["stream"]=false, ["thinking"]=new JObject { ["type"]="disabled" },
                ["temperature"]=0, ["max_tokens"]=maxTokens, ["response_format"]=new JObject { ["type"]="json_object" },
                ["messages"]=new JArray(new JObject { ["role"]="system", ["content"]=instruction }, new JObject { ["role"]="user", ["content"]=input })
            };
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new DeepSeekHttpException((int)response.StatusCode);
            if (response.Content.Headers.ContentType?.MediaType != "application/json") throw new InvalidDataException("Expected JSON response.");
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var canceled = token.Register(() => stream.Dispose());
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = new StringBuilder(); var buffer = new char[4096];
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (count == 0) break;
                if (text.Length + count > 65536) throw new InvalidDataException("JSON response too large.");
                text.Append(buffer, 0, count);
            }
            var json = JObject.Parse(text.ToString());
            var choice = json["choices"]?.First;
            if (choice?["finish_reason"]?.Value<string>() != "stop") throw new EndOfStreamException("Incomplete JSON response.");
            string result = choice?["message"]?["content"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(result)) throw new InvalidDataException("Empty JSON response.");
            return result;
        }
    }
}
