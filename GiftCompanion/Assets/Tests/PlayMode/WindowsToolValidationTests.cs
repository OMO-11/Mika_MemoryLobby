using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.AgentPet.Tools;
using GiftCompanion.Core.Agent;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class WindowsToolValidationTests
    {
        private string folder;
        private AgentSettingsService settings;
        private WindowsAgentPlatform platform;
        [SetUp] public void Setup()
        {
            folder=Path.Combine(Path.GetTempPath(),"GiftToolBoundary-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            settings=new AgentSettingsService(folder);settings.Load(default);platform=new WindowsAgentPlatform(settings);
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
        [TestCase("file:///C:/Windows")][TestCase("javascript:alert(1)")][TestCase("ms-settings:sound")][TestCase("https://user:pass@example.com/")]
        public void UrlToolRejectsCommandsAndEmbeddedCredentials(string url)=>Assert.Throws<ArgumentException>(()=>platform.Prepare("open_url",new JObject{["url"]=url}));
        [Test] public void FilesRequireApprovedRootAndDocumentExtensionAndSensitiveConfirmation()
        {
            string document=Path.Combine(folder,"sample.txt"),script=Path.Combine(folder,"sample.cmd"); File.WriteAllText(document,"fixture");File.WriteAllText(script,"fixture");
            Assert.Throws<ArgumentException>(()=>platform.Prepare("open_file",new JObject{["path"]=document}));
            var next=settings.Value;next.SearchRoots.Add(folder);settings.Save(next,default);
            Assert.That(platform.Prepare("open_file",new JObject{["path"]=document}).Sensitive,Is.True);
            Assert.Throws<ArgumentException>(()=>platform.Prepare("open_file",new JObject{["path"]=script}));
        }
        [Test] public void RootAndArbitrarySettingsAndWindowIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>WindowsAgentPlatform.ValidateSearchRoot(Path.GetPathRoot(folder)));
            Assert.Throws<ArgumentException>(()=>platform.Prepare("open_system_setting",new JObject{["page"]="anything:command"}));
            Assert.Throws<ArgumentException>(()=>platform.Prepare("focus_application",new JObject{["window_id"]="123"}));
        }
        [Test] public async Task SearchIsBoundedToSelectedDirectoriesAndSkipsHiddenSecretFolder()
        {
            Assert.Throws<ArgumentException>(()=>platform.Prepare("search_file",new JObject{["name"]="fixture"}));
            Directory.CreateDirectory(Path.Combine(folder,"Secrets")); File.WriteAllText(Path.Combine(folder,"Secrets","fixture-secret.txt"),"never read");File.WriteAllText(Path.Combine(folder,"fixture-public.txt"),"never read");
            var next=settings.Value;next.SearchRoots.Add(folder);settings.Save(next,default);
            var result=await platform.ExecuteAsync(platform.Prepare("search_file",new JObject{["name"]="fixture"}),default);
            Assert.That(result.Success,Is.True);Assert.That(((JArray)result.Data["files"]).Count,Is.EqualTo(1));Assert.That(result.ToJson().ToString(),Does.Not.Contain("never read").And.Not.Contain("fixture-secret"));
        }
        [Test] public void ClipboardPreparationDoesNotReadOrWriteTheRealClipboard()
        {
            var read=platform.Prepare("get_clipboard",new JObject());Assert.That(read.Sensitive,Is.True);Assert.That(read.Description,Does.Contain("DeepSeek"));
            var write=platform.Prepare("set_clipboard",new JObject{["text"]="受控文本"});Assert.That(write.Sensitive,Is.True);Assert.That(write.Description,Does.Contain("受控文本"));
        }
    }
}
