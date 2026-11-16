using UnityEngine;
using GiftCompanion.SharedUI;
using UI=GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        public CompanionWindowView WindowView { get; private set; }
        CompanionValidationBootstrap windowLoader;
        TMPro.TMP_Text seasideClock;
        bool queuedWindowInput;
        CanvasGroup panelTransition;
        RectTransform transitionPanel;
        UnityEngine.UI.Button reducedMotionButton;
        void UpdateWindowView()
        {
            if(visual==null||!visual.Ready)return;
            if(windowLoader!=visual.Loader)
            {
                if(WindowView!=null)Destroy(WindowView.gameObject);
                windowLoader=visual.Loader;
                var go=new GameObject("Companion window view");go.transform.SetParent(transform,false);
                WindowView=go.AddComponent<CompanionWindowView>();
                WindowView.Bind(windowLoader,root.VisualSettings.Value,visual.ActiveSpaceId,WindowMenu);
                WindowView.CharacterReached+=()=>{menuOpen=false;if(queuedWindowInput){queuedWindowInput=false;OpenInput();}};
                bool seaside=visual.ActiveSpaceId=="mika-seaside";
                clock.transform.parent.gameObject.SetActive(!WindowView.HasWindow&&!seaside);
                if(seaside&&seasideClock==null){
                    seasideClock=UI.Text(canvas.transform,"SeasideClock",System.DateTime.Now.ToString("HH:mm"),new Vector2(-112,-44),new Vector2(176,60),32);
                    seasideClock.rectTransform.anchorMin=seasideClock.rectTransform.anchorMax=Vector2.one;
                    CompanionVisualFoundation.Style(seasideClock,root.VisualSettings.Value.Theme,32);
                    seasideClock.fontSharedMaterial=seasideClock.font.material;
                    seasideClock.overflowMode=TMPro.TextOverflowModes.Overflow;seasideClock.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
                    seasideClock.color=Color.white;
                }
                if(seasideClock!=null)seasideClock.gameObject.SetActive(seaside);
                if(panelName!=null)WindowView.GoWindow();
                RefreshReducedMotionLabel();
            }
            if(WindowView==null)return;
            WindowView.PanelOpen=panelName!=null;
            var sea=visual.Loader.Character.GetComponent<SeasideSpaceView>();
            if(sea!=null){sea.MenuVisible=menuOpen||panelName!=null;sea.ReduceMotion=motionDraft.GetValueOrDefault(root.VisualSettings.Value.ReduceMotion);}
            if(transitionPanel!=null)
            {
                float blend=WindowView.PanelBlend;
                panelTransition.alpha=blend;
                transitionPanel.localScale=Vector3.one*Mathf.Lerp(.97f,1,blend);
                panelTransition.blocksRaycasts=blend>.95f;panelTransition.interactable=blend>.95f;
            }
            if(!WindowView.AtCharacter)menuOpen=panelName==null;
        }
        void WindowMenu(int index)
        {
            switch(index){case 0:_=OpenHistoryAsync();break;case 1:_=OpenDiaryAsync();break;case 2:OpenSpaces();break;case 3:OpenCharacterCard();break;case 4:OpenSettings();break;}
        }
        void BeginPanelTransition(RectTransform panel)
        {
            if(WindowView==null)return;
            panelRevisions[panel]=panelRevisions.ContainsKey(panel)?panelRevisions[panel]+1:1;
            transitionPanel=panel;panelTransition=panel.GetComponent<CanvasGroup>();if(panelTransition==null)panelTransition=panel.gameObject.AddComponent<CanvasGroup>();
            panelTransition.alpha=0;panelTransition.blocksRaycasts=false;
        }
        readonly System.Collections.Generic.Dictionary<RectTransform,int> panelRevisions=new System.Collections.Generic.Dictionary<RectTransform,int>();
        void RetirePanel(RectTransform panel)
        {
            if(panel==null||!panel.gameObject.activeSelf)return;
            if(WindowView==null||WindowView.ReduceMotion){panel.gameObject.SetActive(false);return;}
            var group=panel.GetComponent<CanvasGroup>();if(group==null){panel.gameObject.SetActive(false);return;}
            group.blocksRaycasts=false;group.interactable=false;
            foreach(var field in panel.GetComponentsInChildren<TMPro.TMP_InputField>())field.DeactivateInputField();
            if(transitionPanel==panel)transitionPanel=null;
            int revision=panelRevisions.ContainsKey(panel)?panelRevisions[panel]+1:1;panelRevisions[panel]=revision;
            StartCoroutine(FadePanelOut(panel,group,revision));
        }
        System.Collections.IEnumerator FadePanelOut(RectTransform panel,CanvasGroup group,int revision)
        {
            float alpha=group.alpha;
            for(float elapsed=0;elapsed<.16f;elapsed+=Time.unscaledDeltaTime)
            {
                if(panel==null||panelRevisions[panel]!=revision)yield break;
                group.alpha=Mathf.Lerp(alpha,0,elapsed/.16f);panel.localScale=Vector3.one*Mathf.Lerp(1,.97f,elapsed/.16f);yield return null;
            }
            if(panel!=null&&panelRevisions[panel]==revision)panel.gameObject.SetActive(false);
        }
        public void SetReducedWindowMotion(bool value)
        {
            var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<Core.Config.CompanionVisualSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(root.VisualSettings.Value));copy.ReduceMotion=value;
            root.VisualSettings.Save(copy,root.ApplicationToken);if(WindowView!=null)WindowView.ReduceMotion=value;
            RefreshReducedMotionLabel();
        }
        void RefreshReducedMotionLabel(){if(reducedMotionButton!=null)reducedMotionButton.GetComponentInChildren<TMPro.TMP_Text>().text="减少动态效果："+(motionDraft.GetValueOrDefault(root.VisualSettings.Value.ReduceMotion)?"已开启":"已关闭")+"（保存后保留）";}
    }
}
