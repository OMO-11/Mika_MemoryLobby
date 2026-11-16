using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Agent;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.LLM
{
    public sealed partial class DeepSeekApiClient
    {
        public async Task<AgentReply> CompleteAsync(JArray messages, JArray tools, bool finalOnly, Action<string> delta, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds)); var token = timeout.Token;
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            var body = new JObject { ["model"] = model, ["stream"] = true, ["thinking"] = new JObject { ["type"] = "disabled" }, ["max_tokens"] = 1024, ["messages"] = messages, ["tools"] = tools, ["tool_choice"] = finalOnly ? "none" : "auto" };
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new DeepSeekHttpException((int)response.StatusCode);
            if (response.Content.Headers.ContentType?.MediaType != "text/event-stream") throw new InvalidDataException("Expected tool SSE.");
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var canceled = token.Register(() => stream.Dispose()); using var reader = new StreamReader(stream, Encoding.UTF8);
            var parser = new AgentSseAccumulator(finalOnly); var data = new StringBuilder();
            while (true)
            {
                token.ThrowIfCancellationRequested(); string line;
                try { line = await reader.ReadLineAsync().ConfigureAwait(false); }
                catch (Exception error) when (token.IsCancellationRequested && (error is IOException || error is ObjectDisposedException)) { throw new OperationCanceledException(token); }
                token.ThrowIfCancellationRequested();
                if (line == null) throw new EndOfStreamException("Tool stream ended before DONE.");
                if (line.Length > 65536) throw new InvalidDataException("Tool SSE line too large.");
                if (line.Length != 0)
                {
                    if (line.StartsWith("data:", StringComparison.Ordinal)) { if (data.Length > 0) data.Append('\n'); data.Append(line.Substring(5).TrimStart(' ')); if (data.Length > 65536) throw new InvalidDataException("Tool SSE too large."); }
                    continue;
                }
                if (data.Length == 0) continue;
                string payload = data.ToString(); data.Clear();
                if (payload.Trim() == "[DONE]") return parser.Finish();
                string text = parser.Feed(JObject.Parse(payload));
                if (finalOnly && text.Length > 0) delta?.Invoke(text);
            }
        }
    }
    // Parsing is separate so malformed, fragmented or unfinished arguments can never reach execution.
    public sealed class AgentSseAccumulator
    {
        private readonly bool finalOnly;
        private readonly SortedDictionary<int, AgentCall> calls = new SortedDictionary<int, AgentCall>();
        private readonly StringBuilder text = new StringBuilder();
        private string finish;
        public AgentSseAccumulator(bool finalOnly) => this.finalOnly = finalOnly;
        public string Feed(JObject packet)
        {
            if (packet["error"] != null) throw new InvalidDataException("Tool stream error.");
            var choice = packet["choices"]?.First; if (choice == null) return "";
            if (finish != null) throw new InvalidDataException("Data after tool finish.");
            var delta = choice["delta"]; string content = (string)delta?["content"] ?? "";
            if (text.Length + content.Length > 16000) throw new InvalidDataException("Tool reply too large."); text.Append(content);
            if (delta?["tool_calls"] is JArray fragments)
            {
                if (finalOnly) throw new InvalidDataException("Unexpected final tool call.");
                foreach (var fragment in fragments)
                {
                    if (fragment["index"]?.Type != JTokenType.Integer) throw new InvalidDataException("Missing tool index.");
                    int index = (int)fragment["index"]; if (index < 0 || index > 7) throw new InvalidDataException("Too many tool calls.");
                    if (!calls.TryGetValue(index, out var call)) calls[index] = call = new AgentCall { Id = "", Name = "", Arguments = "" };
                    call.Id += (string)fragment["id"] ?? ""; call.Name += (string)fragment["function"]?["name"] ?? ""; call.Arguments += (string)fragment["function"]?["arguments"] ?? "";
                    if (call.Id.Length > 128 || call.Name.Length > 64 || call.Arguments.Length > 8192) throw new InvalidDataException("Tool call too large.");
                    if (fragment["type"] != null && (string)fragment["type"] != "function") throw new InvalidDataException("Unknown tool type.");
                }
            }
            if (choice["finish_reason"]?.Type == JTokenType.String) finish = (string)choice["finish_reason"];
            return content;
        }
        public AgentReply Finish()
        {
            if ((calls.Count > 0 && finish != "tool_calls") || (calls.Count == 0 && finish != "stop") || calls.Values.Any(c => string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name)) || calls.Values.Select(c => c.Id).Distinct().Count() != calls.Count) throw new EndOfStreamException("Incomplete tool response.");
            var reply = new AgentReply { Text = text.ToString() }; reply.Calls.AddRange(calls.Values); return reply;
        }
    }
}
