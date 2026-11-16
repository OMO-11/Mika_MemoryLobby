using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#endif
namespace GiftCompanion.Validation
{
    public sealed class V2ReadingProbe:MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
        void Check(bool value,string name){checks[name]=value;if(!value)throw new Exception(name);}
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
        static void Replace(object owner,string name,object value)=>owner.GetType().GetField("<"+name+">k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
        async void Start()
        {
            var args=Environment.GetCommandLineArgs();int marker=Array.IndexOf(args,"-v2-reading-probe");if(marker<0||marker+1>=args.Length)return;
            string output=Path.GetFullPath(args[marker+1]);Directory.CreateDirectory(output);string error=null;var root=GetComponent<AppRoot>();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(55));var ct=timeout.Token;
            try
            {
                await root.Initialization;CompanionScreen screen=null;
                while(screen==null||!screen.IsReady||screen.WindowView==null){ct.ThrowIfCancellationRequested();screen=FindFirstObjectByType<CompanionScreen>();await Task.Delay(20,ct);}
                await Capture(output+"/主场景.png");
                var input=Field<TMP_InputField>(screen,"input");screen.OpenInput();input.text="老师，今天也一起慢慢聊吧。";await Task.Delay(180,ct);await Capture(output+"/透明输入.png");screen.Back();await Task.Delay(150,ct);screen.OpenInput();Check(screen.InputDraft=="老师，今天也一起慢慢聊吧。","draftSurvivesCancel");screen.Back();
                Check(input.GetComponent<Image>().color.a==0,"transparentInput");
                var fixture=new Fixture();var core=new CharacterCore(root.Character.Identity,root.Character.State,root.Database,fixture,root.Memory,root.Logger,root.ApplicationToken);Replace(root,"Character",core);
                var cfg=root.Config.Configuration.Copy();cfg.VoiceEnabled=true;cfg.BgmVolume=.12f;await root.Config.SaveAsync(cfg,ct);
                var music=FindFirstObjectByType<Companion.Bgm.BgmManager>();var visual=FindFirstObjectByType<CompanionPresentation>();
                screen.VoicePlayer.Configure(fixture,root.AudioCache,root.VoiceSettings,music,()=>visual.Loader.Speech,root.Logger,.1f);
                await screen.SendAsync("隔离验证：字幕和历史排版");
                while(!screen.VoicePlayer.IsPlaying){ct.ThrowIfCancellationRequested();await Task.Delay(10,ct);}
                await Task.Delay(250,ct);await Capture(output+"/无框字幕.png");var subtitle=Field<TMP_Text>(screen,"subtitle");Check(subtitle.text.Length>0&&subtitle.transform.parent.GetComponent<Image>().color.a==0,"subtitleWithoutPlate");Check(Field<CanvasGroup>(screen,"subtitleFade").alpha>.7f&&subtitle.textInfo.characterCount>0,"subtitleActuallyVisible");
                Check(!Field<TMP_Text>(screen,"speaker").gameObject.activeSelf,"noNameBar");await screen.VoicePlayer.Completion;await screen.VoicePlayer.DrainAsync();
                cfg=root.Config.Configuration.Copy();cfg.VoiceEnabled=false;await root.Config.SaveAsync(cfg,ct);
                await screen.SendAsync("隔离验证：长记录与缺缓存");
                string song=Path.Combine(root.Config.UserDataPath,"验证钢琴短曲.wav");File.WriteAllBytes(song,Fixture.Wav());await screen.ImportMusicAsync(song);
                var track=root.Library.Music.Tracks.Last();await screen.SelectMusicAsync(track.Id);await Task.Delay(300,ct);
                screen.RevealMusicControls(false);screen.FocusMusicControls();await Task.Delay(250,ct);Check(screen.MusicControlsVisible,"keyboardFocusRevealsMusic");Check(screen.MusicTitle==track.Title,"actualSongTitle");
                string songId=screen.CurrentMusicId;screen.ToggleMusicEmblem();Check(screen.CurrentMusicId==songId&&screen.SwissEmblem,"emblemDoesNotChangeSong");
                screen.ToggleMusicPause();float playhead=music.Source.time;await Task.Delay(150,ct);Check(screen.MusicPaused&&Mathf.Abs(music.Source.time-playhead)<.04f,"musicPauseKeepsPosition");screen.ToggleMusicPause();
                await Capture(output+"/音乐中枢.png");screen.RevealMusicControls(false);EventSystem.current.SetSelectedGameObject(null);
                screen.OpenDataSettings();Field<RectTransform>(screen,"settingsPanel").GetComponentsInChildren<Button>().Single(b=>b.name=="MusicFiles").onClick.Invoke();Check(screen.ActivePanel=="Music","musicManagementRemainsReachable");
                await screen.ImportMusicAsync(Path.Combine(root.Config.UserDataPath,"missing.wav"));Check(screen.CurrentMusicId==songId,"invalidMusicKeepsCurrentTrack");screen.Back();
                screen.OpenMenu();while(!screen.WindowView.AtWindow){ct.ThrowIfCancellationRequested();screen.WindowView.Focused=true;await Task.Delay(20,ct);}
                await screen.OpenHistoryAsync();await Task.Delay(300,ct);Field<ScrollRect>(screen,"historyScroll").verticalNormalizedPosition=1;await Capture(output+"/聊天记录.png");
                var history=Field<RectTransform>(screen,"historyPanel");Check(history.GetComponent<Image>().color.a==0,"floatingHistory");Check(screen.VisibleHistoryCount==2,"sharedRealHistory");
                var replayButtons=history.GetComponentsInChildren<Button>().Where(button=>button.name=="Replay").ToArray();Check(replayButtons.Any(button=>button.interactable)&&replayButtons.Any(button=>!button.interactable),"cacheMissingDisablesReplay");
                int calls=fixture.VoiceCalls;replayButtons.First(button=>button.interactable).onClick.Invoke();await Task.Delay(120,ct);Check(screen.VoicePlayer.IsReplaying,"cachedReplayStarts");screen.OpenMenu();Check(!screen.VoicePlayer.IsReplaying&&fixture.VoiceCalls==calls,"leavingHistoryStopsReplayWithoutTts");
                var diary=new RelationshipDiary((IDiaryStore)root.Database,fixture,()=>root.Character.Identity,root.Config.UserDataPath);Replace(root,"Diary",diary);await diary.InitializeAsync(ct);
                await screen.OpenDiaryAsync();await Task.Delay(250,ct);await Capture(output+"/日记空态.png");Check((await diary.ListAsync(ct)).Count==0,"diaryNotGeneratedOnOpen");
                fixture.HoldDiary=true;var pending=screen.GenerateDiaryAsync();await Task.Delay(100,ct);Field<RectTransform>(screen,"diaryPanel").Find("Cancel").GetComponent<Button>().onClick.Invoke();await pending;await Task.Delay(80,ct);Check(!Field<CancellationTokenSource>(screen,"panelCancellation").IsCancellationRequested,"cancelDiaryReopensUsablePage");fixture.HoldDiary=false;Check((await diary.ListAsync(ct)).Count==0,"cancelDiaryDoesNotPublish");
                fixture.HoldDiary=true;pending=screen.GenerateDiaryAsync();await Task.Delay(80,ct);screen.OpenDataSettings();await pending;fixture.HoldDiary=false;Check(screen.ActivePanel=="Settings"&&!root.PlayerWorkBusy&&(await diary.ListAsync(ct)).Count==0,"leavingDiaryCancelsGenerationWithoutBlockingNavigation");
                await screen.OpenDiaryAsync();await screen.GenerateDiaryAsync();var entries=await diary.ListAsync(ct);Check(entries.Count==1,"manualAllHistoryDiary");
                var entry=entries[0];Check(entry.TurnCount==2,"diaryCoversRetainedHistory");string md=diary.ExportPath(entry);Check(File.Exists(md),"databaseAndMarkdownSaved");
                File.AppendAllText(md,"\n验证批注保留。\n");await screen.OpenDiaryAsync();Check(File.ReadAllText(md).Contains("验证批注保留"),"reopeningPreservesNotes");
                await Task.Delay(300,ct);await Capture(output+"/浮字日记.png");var scroll=Field<ScrollRect>(screen,"diaryScroll");
                screen.ContinueDiaryReading();Check(scroll.verticalNormalizedPosition<.95f,"continueReadingMovesDown");float reading=scroll.verticalNormalizedPosition;await Capture(output+"/日记继续阅读.png");screen.OpenMenu();await screen.OpenDiaryAsync();Check(Mathf.Abs(scroll.verticalNormalizedPosition-reading)<.04f,"diaryPositionRestored");
                Check(Field<RectTransform>(screen,"diaryPanel").GetComponent<Image>().color.a==0&&scroll.GetComponent<Image>().color.a==0,"diaryHasNoBoard");
                var savedDisplay=root.Display.Value.Copy();var large=savedDisplay.Copy();large.DiaryFontSize=30;large.DiaryLineSpacing=20;root.Display.Save(large,ct);
                Screen.SetResolution(960,540,FullScreenMode.Windowed);await Task.Delay(300,ct);await screen.OpenDiaryAsync();await Task.Delay(300,ct);Canvas.ForceUpdateCanvases();
                var diaryText=Field<RectTransform>(screen,"diaryContent").GetComponentInChildren<TMP_Text>();diaryText.ForceMeshUpdate();
                Check(diaryText.fontSize==30&&diaryText.textInfo.lineInfo.Where(line=>line.characterCount>0).All(line=>line.maxAdvance<=diaryText.rectTransform.rect.width+2),"largeDiaryWrapsInSmallWindow");
                Check(scroll.content.rect.height>scroll.viewport.rect.height,"longDiaryRemainsScrollable");await Capture(output+"/小窗口大字号.png");
                root.Display.Save(savedDisplay,ct);Screen.SetResolution(1280,720,FullScreenMode.Windowed);await Task.Delay(300,ct);
                var turns=await ((IConversationStore)root.Database).LoadTurnsAsync(10,true,ct);await root.Character.ForgetTurnAsync(turns[0].RequestId,ct);await screen.OpenDiaryAsync();
                Check(!Field<RectTransform>(screen,"diaryContent").GetComponentInChildren<TMP_Text>().text.Contains("这是隔离验证日记"),"forgottenSourceHidesDerivedBody");await Capture(output+"/日记来源失效.png");
                Check(root.Character==core,"sameCoreAcrossReading");Check(root.Voice.OwnedProcessId==0,"noRealVoiceProcess");await root.ShutdownAsync();Check(root.ShutdownSucceeded,"savedShutdown");
            }
            catch(Exception e){error=e.ToString();}
            bool passed=error==null&&checks.Values.All(value=>value);AtomicDocument.WriteJson(output+"/native.json",new{passed,error,checks,fixtures="Isolated generated conversations, WAV and diary; no real player data, network LLM or GPT-SoVITS",graphics=SystemInfo.graphicsDeviceName},default,false);
            if(root!=null)await root.ShutdownAsync();Application.Quit(passed?0:1);
        }
        Task Capture(string path){var done=new TaskCompletionSource<bool>();StartCoroutine(CaptureFrame(path,done));return done.Task;}
        System.Collections.IEnumerator CaptureFrame(string path,TaskCompletionSource<bool> done)
        {yield return new WaitForEndOfFrame();Texture2D frame=null;try{frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();File.WriteAllBytes(path,frame.EncodeToPNG());done.TrySetResult(true);}catch(Exception e){done.TrySetException(e);}finally{if(frame!=null)Destroy(frame);}}
        sealed class Fixture:ILLMService,IMemoryLLMService,IVoiceSynthesizer
        {
            public bool IsConfigured=>true;public string ProfileId=>"zh:isolated-v2-reading";public int VoiceCalls;public bool HoldDiary;
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct)
            {await Task.Delay(30,ct);yield return new LLMChunk("<META emotion=\"happy\" action=\"none\">\n老师，这是隔离验证资料。让文字留一点空白，我们可以慢慢看。"+(request.Message.Contains("长记录")?string.Concat(Enumerable.Repeat("这段长记录用于核对换行、滚动和缓存缺失状态。",9)):""));}
            public async Task<byte[]> SynthesizeAsync(string text,CancellationToken ct){VoiceCalls++;await Task.Delay(30,ct);return Wav();}
            public async Task<string> CompleteJsonAsync(string instruction,string input,int maxTokens,CancellationToken ct)
            {
                while(HoldDiary)await Task.Delay(20,ct);
                if(instruction.Contains("paragraphs"))return Newtonsoft.Json.JsonConvert.SerializeObject(new{paragraphs=Enumerable.Range(0,6).Select(i=>"这是隔离验证日记。老师，我们刚才用示例文字聊了排版和阅读。这些段落只用来检查字号、行距、滚动与继续阅读，不记录真实的共同经历。让每句话之间留出一点空白，阅读时就可以慢慢看，也可以随时回到上一段。(*´▽`*)".Replace(i>1?"(*´▽`*)":"不替换", "")).ToArray()});
                return "{\"summary\":\"隔离示例中聊了透明字幕与阅读排版，没有真实玩家经历。\"}";
            }
            public static byte[] Wav()
            {int count=24000;using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);for(int i=0;i<count;i++)writer.Write((short)(Math.Sin(i*2*Math.PI*220/24000)*300));return stream.ToArray();}
        }
#endif
    }
}

