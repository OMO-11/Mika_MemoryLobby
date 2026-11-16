using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.AgentPet;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace GiftCompanion.Tests
{
    public sealed class PetScreenTests
    {
        [SetUp] public void Setup() => PlayTestData.Begin();
        private static IEnumerator Await(Task task) { float until=Time.realtimeSinceStartup+30; while(!task.IsCompleted) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(until));yield return null; } if(task.IsFaulted)throw task.Exception; }
        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity"); float end=Time.realtimeSinceStartup+30;
            while(AppRoot.Instance?.IsReady!=true||AppRoot.Instance.Controller.IsSwitching||SceneManager.GetActiveScene().name!="Home") { Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null; }
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.AgentPet));
            while(Object.FindFirstObjectByType<PetScreen>()?.Ready!=true) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(end+20));yield return null; }
        }
        [UnityTest] public IEnumerator PetInputKeepsNoKeyDraftAndNeverStartsVoice()
        {
            yield return Boot();var root=AppRoot.Instance;var screen=Object.FindFirstObjectByType<PetScreen>();
            screen.OpenInput();Assert.That(screen.IsEditing,Is.True);screen.Hide();Assert.That(screen.IsOpen,Is.False);
            yield return Await(screen.SendAsync("无Key时保留的桌面草稿"));Assert.That(screen.InputDraft,Does.Contain("桌面草稿"));
            Assert.That(root.Voice.OwnedProcessId,Is.Zero);screen.OpenSettings();Assert.That(screen.PanelName,Is.EqualTo("settings"));
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Home));yield return null;Assert.That(screen==null,Is.True);
        }
        [UnityTest] public IEnumerator PetStreamsThroughSharedCoreAndHideDoesNotCancelTurn()
        {
            yield return Boot();var root=AppRoot.Instance;var fake=new HeldReply();
            var core=new CharacterCore(root.Character.Identity,root.Character.State,root.Database,fake,root.Memory,root.Logger,root.ApplicationToken);
            yield return Await(core.SwitchModeAsync(CharacterMode.AgentPet));typeof(AppRoot).GetField("<Character>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(root,core);
            var screen=Object.FindFirstObjectByType<PetScreen>();var pending=screen.SendAsync("在桌面继续聊天");yield return Await(fake.First.Task);
            yield return null;Assert.That(screen.OutputText,Does.Contain("第一段"));Assert.That(pending.IsCompleted,Is.False);screen.Hide();fake.Release.SetResult(true);yield return Await(pending);
            Assert.That(screen.IsOpen,Is.False);Assert.That(screen.LastResult.Success,Is.True);Assert.That(screen.OutputText,Is.EqualTo("第一段，后半段。"));
            var history=root.History.ReadPageAsync(null,default);yield return Await(history);Assert.That(history.Result.Turns.Count,Is.EqualTo(1));Assert.That(history.Result.Turns[0].Source,Is.EqualTo(InteractionSource.AgentPet));
            Assert.That(root.Voice.OwnedProcessId,Is.Zero);
        }
        [UnityTest] public IEnumerator PermissionIsExplicitAndHidingDeclinesIt()
        {
            yield return Boot(); var screen=Object.FindFirstObjectByType<PetScreen>();
            var permission=screen.ConfirmToolAsync(new GiftCompanion.Core.Agent.AgentPermission {Tool="get_clipboard",Description="受控测试文本，不读取真实剪贴板"},default);
            Assert.That(permission.IsCompleted,Is.False);Assert.That(screen.PanelName,Is.EqualTo("permission"));
            screen.Hide();yield return Await(permission);Assert.That(permission.Result,Is.False);Assert.That(screen.IsOpen,Is.False);
            permission=screen.ConfirmToolAsync(new GiftCompanion.Core.Agent.AgentPermission {Tool="set_clipboard",Description="假数据"},default);
            screen.AnswerPermission(true);yield return Await(permission);Assert.That(permission.Result,Is.True);
        }
        private sealed class HeldReply:ILLMService
        {
            public bool IsConfigured=>true;public readonly TaskCompletionSource<bool> First=new TaskCompletionSource<bool>(),Release=new TaskCompletionSource<bool>();
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct)
            {yield return new LLMChunk("第一段，");First.TrySetResult(true);await Release.Task;ct.ThrowIfCancellationRequested();yield return new LLMChunk("后半段。");}
        }
        [UnityTearDown] public IEnumerator Cleanup() {if(AppRoot.Instance!=null) {yield return Await(AppRoot.Instance.ShutdownAsync());Object.Destroy(AppRoot.Instance.gameObject);yield return null;}PlayTestData.End();}
    }
}
