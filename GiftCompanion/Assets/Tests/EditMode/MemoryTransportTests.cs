using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.LLM;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class MemoryTransportTests
    {
        private sealed class Handler:HttpMessageHandler
        {
            public JObject Request;
            public string Finish="stop";
            public bool Stream;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
            {
                Request=JObject.Parse(await request.Content.ReadAsStringAsync());
                string body=Stream ? "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n"
                    :new JObject { ["choices"]=new JArray(new JObject { ["finish_reason"]=Finish,["message"]=new JObject { ["content"]="{\"facts\":[]}" } }) }.ToString();
                return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(body,Encoding.UTF8,Stream?"text/event-stream":"application/json")};
            }
        }
        [Test] public async Task JsonTransportUsesLowBudgetAndRejectsDeclaredTruncation()
        {
            var handler=new Handler();using var llm=new LLMService("fake-key",handler:handler);
            Assert.That(await llm.CompleteJsonAsync("输出 JSON","input",1024,default),Is.EqualTo("{\"facts\":[]}"));
            Assert.That(handler.Request["response_format"]?["type"]?.Value<string>(),Is.EqualTo("json_object"));
            Assert.That(handler.Request["max_tokens"]?.Value<int>(),Is.EqualTo(1024));
            Assert.That(handler.Request["stream"]?.Value<bool>(),Is.False);
            handler.Finish="length";
            var failure=Assert.ThrowsAsync<LLMServiceException>(()=>llm.CompleteJsonAsync("输出 JSON","input",1024,default));
            Assert.That(failure.Code,Is.EqualTo("StreamTruncated"));
        }
        [Test] public async Task StreamingRequestRetainsHistoryRolesAndRejectsLengthFinish()
        {
            var handler=new Handler {Stream=true};using var llm=new LLMService("fake-key",handler:handler);
            var request=new DialogueRequest("current",InteractionSource.Companion,"identity",new[] {new LLMMessage("user","old-user"),new LLMMessage("assistant","old-assistant")});
            string code=null;
            try {await foreach(var unused in llm.StreamDialogueAsync(request,default)) { }}catch(LLMServiceException error){code=error.Code;}
            Assert.That(code,Is.EqualTo("StreamTruncated"));
            var messages=(JArray)handler.Request["messages"];
            Assert.That(messages.Count,Is.EqualTo(4));
            Assert.That(messages[2]["role"]?.Value<string>(),Is.EqualTo("assistant"));
            Assert.That(messages[3]["content"]?.Value<string>(),Is.EqualTo("current"));
        }
    }
}
