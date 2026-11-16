using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;
using UnityEngine;

namespace GiftCompanion.Validation
{
    // Explicit opt-in developer UI, removed from the normal presentation flow.
    public sealed class Phase2DevelopmentPanel:MonoBehaviour
    {
        private string input="",output="",lastRequestId,kind="fact";
        private bool busy,visible=true;
        private Vector2 scroll;
        private Font font;
        private IReadOnlyList<MemoryRecord> records=Array.Empty<MemoryRecord>();
        private void Awake()=>enabled=Array.IndexOf(Environment.GetCommandLineArgs(),"-phase2-dev")>=0;
        public void Open(){enabled=true;visible=true;}
        private void OnGUI()
        {
            var root=AppRoot.Instance;if(root==null||!root.IsReady)return;
            if(!visible){if(GUI.Button(new Rect(16,16,100,28),"Dev Memory"))visible=true;return;}
            if(font==null)font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",16);
            Font previous=GUI.skin.font;bool previousEnabled=GUI.enabled;GUI.skin.font=font;
            try
            {
                GUILayout.BeginArea(new Rect(18,18,Mathf.Min(Screen.width-36,660),Mathf.Min(Screen.height-36,640)),GUI.skin.box);
                GUILayout.Label("Phase 2 记忆验收（开发入口）");
                if(GUILayout.Button("收起"))visible=false;
                scroll=GUILayout.BeginScrollView(scroll);
                GUI.enabled=!busy&&root.Character.CurrentMode!=CharacterMode.Home&&!root.Controller.IsSwitching;
                input=GUILayout.TextArea(input,12000,GUILayout.Height(60));
                if(GUILayout.Button("发送"))Submit();
                GUI.enabled=false;GUILayout.TextArea(output,GUILayout.MinHeight(55));
                GUI.enabled=!busy;
                if(GUILayout.Button("不记上一轮")&&!string.IsNullOrEmpty(lastRequestId))Run(()=>root.Character.ForgetTurnAsync(lastRequestId,root.ApplicationToken));
                GUILayout.BeginHorizontal();
                foreach(string choice in new[]{"fact","memory","loop"})
                    if(GUILayout.Button(choice=="fact"?"事实":choice=="memory"?"事件":"未完成")){kind=choice;Run(Refresh);}
                GUILayout.EndHorizontal();
                if(GUILayout.Button("刷新记忆"))Run(Refresh);
                if(GUILayout.Button("重试失败的提取 / 摘要"))Run(()=>((ISharedMemoryService)root.Memory).RetryFailedAsync(root.ApplicationToken));
                foreach(var record in records)
                {
                    GUILayout.Label((string.IsNullOrEmpty(record.Key)?"":record.Key+"：")+record.Content);
                    if(GUILayout.Button("忘记这条记忆"))Run(()=>root.Character.ForgetMemoryAsync(record.Kind,record.Id,root.ApplicationToken));
                }
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
            finally{GUI.skin.font=previous;GUI.enabled=previousEnabled;}
        }
        private async void Submit()
        {
            if(busy||string.IsNullOrWhiteSpace(input))return;
            var root=AppRoot.Instance;var mode=root.Character.CurrentMode;var modeToken=root.Controller.ActiveModeToken;
            string message=input;input="";output="";busy=true;lastRequestId=null;
            try
            {
                using var linked=CancellationTokenSource.CreateLinkedTokenSource(modeToken,root.ApplicationToken);
                var text=new StringBuilder();
                await foreach(var chunk in root.Character.StreamMessageAsync(message,mode==CharacterMode.AgentPet?InteractionSource.AgentPet:InteractionSource.Companion,linked.Token))
                {
                    if(modeToken.IsCancellationRequested||mode!=root.Character.CurrentMode)break;
                    if(chunk.IsFinal){if(!chunk.Result.Success)output="本轮未完成："+chunk.Result.ErrorCode;else lastRequestId=chunk.Result.RequestId;}
                    else{text.Append(chunk.Text);output=text.ToString();}
                }
                await Refresh();
            }
            catch(OperationCanceledException){output="本轮已取消。";}
            catch(Exception error){output="暂时无法完成，请重试。";root.Logger?.Error("phase2 dev input failed",error);}
            finally{busy=false;}
        }
        private Task Refresh()
        {
            return RefreshCore();
            async Task RefreshCore(){records=await ((ISharedMemoryService)AppRoot.Instance.Memory).ListActiveAsync(kind,AppRoot.Instance.ApplicationToken);}
        }
        private async void Run(Func<Task> action)
        {
            if(busy)return;busy=true;
            try{await action();await Refresh();}
            catch(Exception error){output="操作未完成，请重试。";AppRoot.Instance?.Logger?.Error("phase2 dev memory command failed",error);}
            finally{busy=false;}
        }
        private void OnDestroy(){if(font!=null)Destroy(font);}
    }
}
