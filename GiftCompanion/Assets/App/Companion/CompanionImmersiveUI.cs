using System;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI=GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        RectTransform musicHub;
        CanvasGroup musicControls;
        TMP_Text musicTitle,diaryMeta;
        Image emblemImage,pauseImage;
        Slider hubVolume;
        bool swissEmblem,hubPinned,hubVolumePending,hubSaving;
        float hubSaveAt;
        Task hubSaveTask=Task.CompletedTask;
        static TMP_FontAsset trackFont;
        int inputRevision;
        public bool MusicPaused => music?.Paused==true;
        public string MusicTitle => musicTitle==null?"":musicTitle.text;
        public bool MusicControlsVisible => musicControls!=null&&musicControls.alpha>.9f;
        public bool SwissEmblem => swissEmblem;
        public void ToggleMusicPause(){if(music!=null&&music.HasPlaylist)music.SetPaused(!music.Paused);}
        public void ToggleMusicEmblem(){swissEmblem=!swissEmblem;if(emblemImage!=null)emblemImage.sprite=CompanionVisualFoundation.Icon(swissEmblem?"icon-swiss-roll":"icon-double-star");}
        public void RevealMusicControls(bool visible){hubPinned=visible;}
        public void FocusMusicControls(){if(musicHub!=null&&panelName==null&&!inputOpen)EventSystem.current?.SetSelectedGameObject(musicHub.Find("RevealMusic").gameObject);}
        bool HasSelectedControl()
        {var selected=EventSystem.current?.currentSelectedGameObject;var selectable=selected==null?null:selected.GetComponent<Selectable>();return selectable!=null&&selectable.IsActive()&&selectable.IsInteractable();}
        void ReleaseMusicFocus()
        {if(musicHub==null)return;hubPinned=false;var selected=EventSystem.current?.currentSelectedGameObject;if(selected!=null&&selected.transform.IsChildOf(musicHub))EventSystem.current.SetSelectedGameObject(null);}
        void BuildImmersiveUI()
        {
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            StyleTransparentInput();
            var backdrop=subtitle.transform.parent.GetComponent<Image>();backdrop.color=Color.clear;
            var subtitleRect=(RectTransform)subtitle.transform.parent;subtitleRect.anchoredPosition=new Vector2(0,-244);subtitleRect.sizeDelta=new Vector2(1160,140);
            subtitle.rectTransform.anchoredPosition=Vector2.zero;subtitle.rectTransform.sizeDelta=new Vector2(1110,112);
            subtitle.font=CompanionVisualFoundation.Font;subtitle.alignment=TextAlignmentOptions.BottomLeft;subtitle.color=Color.white;subtitle.outlineWidth=.12f;subtitle.outlineColor=new Color32(25,31,56,230);
            speaker.gameObject.SetActive(false);FloatButton(readTextButton,16);readTextButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(493,-321);
            BuildMusicHub();BuildReadingPage(historyPanel,"聊天记录",0,historyScroll,historyNote);
            var musicFiles=UI.Button(settingsPanel,"MusicFiles","音乐文件管理",new Vector2(0,240),new Vector2(300,30),OpenMusic);FloatButton(musicFiles,17);
            musicPanel.Find("Title").GetComponent<TMP_Text>().text="音乐文件管理";
            BuildReadingPage(diaryPanel,"相处日记",1,diaryScroll,diaryNote);
            Place(olderButton.GetComponent<RectTransform>(),548,613,120,34);FloatButton(olderButton,16);
            Place(newerButton.GetComponent<RectTransform>(),685,613,120,34);FloatButton(newerButton,16);
            Place(diaryPanel.Find("Previous") as RectTransform,548,613,100,34);FloatButton(diaryPanel.Find("Previous").GetComponent<Button>(),16);
            Place(diaryPanel.Find("Next") as RectTransform,650,613,100,34);FloatButton(diaryPanel.Find("Next").GetComponent<Button>(),16);
            Place(diaryPanel.Find("Generate") as RectTransform,1020,248,155,40);diaryPanel.Find("Generate").GetComponentInChildren<TMP_Text>(true).text="回望至今";FloatButton(diaryPanel.Find("Generate").GetComponent<Button>(),17);
            Place(diaryPanel.Find("Folder") as RectTransform,1008,613,177,34);diaryPanel.Find("Folder").GetComponentInChildren<TMP_Text>(true).text="打开日记文件 ↗";FloatButton(diaryPanel.Find("Folder").GetComponent<Button>(),15);
            Place(diaryPanel.Find("Cancel") as RectTransform,820,658,160,32);FloatButton(diaryPanel.Find("Cancel").GetComponent<Button>(),15);diaryPanel.Find("Cancel").gameObject.SetActive(false);
            var continueButton=UI.Button(diaryPanel,"ContinueReading","继续阅读 ↓",Vector2.zero,new Vector2(160,36),ContinueDiaryReading);Place(continueButton.GetComponent<RectTransform>(),797,613,165,34);FloatButton(continueButton,16);
            diaryMeta=UI.Text(diaryPanel,"Range","开始相伴 → 现在",Vector2.zero,new Vector2(620,32),15,TextAlignmentOptions.Left);Place(diaryMeta.rectTransform,548,300,620,30);StyleInk(diaryMeta,15,.75f);
        }
        void StyleTransparentInput()
        {
            inputPanel.GetComponent<Image>().color=Color.clear;inputPanel.sizeDelta=new Vector2(1130,125);inputPanel.anchoredPosition=new Vector2(0,-247);
            input.fontAsset=CompanionVisualFoundation.Font;input.textComponent.font=CompanionVisualFoundation.Font;input.textComponent.color=Color.white;
            input.onFocusSelectAll=false;
            var placeholder=(TMP_Text)input.placeholder;placeholder.font=CompanionVisualFoundation.Font;placeholder.color=new Color(1,.94f,.92f,.6f);
            input.selectionColor=new Color(.92f,.76f,.83f,.3f);input.caretColor=Color.white;input.customCaretColor=true;
            FloatButton(inputPanel.Find("Send").GetComponent<Button>(),20);
            inputPanel.Find("Send").GetComponent<RectTransform>().anchoredPosition=new Vector2(480,8);
            var inputHint=inputPanel.Find("InputHint").GetComponent<TMP_Text>();StyleInk(inputHint,14,.7f);
            var line=UI.Panel(inputPanel,"Underline",new Vector2(-38,-30),new Vector2(900,1),new Color(1,.9f,.9f,.3f),false);
            var viewport=inputPanel.Find("Text Viewport").GetComponent<RectTransform>();viewport.sizeDelta=new Vector2(950,73);viewport.anchoredPosition=new Vector2(-75,8);
            input.textComponent.rectTransform.sizeDelta=new Vector2(950,73);placeholder.rectTransform.sizeDelta=new Vector2(950,73);
        }
        static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x-640,360-y);rect.sizeDelta=new Vector2(width,height);}
        void StyleInk(TMP_Text text,float size,float alpha=1)
        {CompanionVisualFoundation.Style(text,root.VisualSettings.Value.Theme,size);text.color=CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Ink,alpha);}
        void FloatButton(Button button,float size=18)
        {var image=button.GetComponent<Image>();if(image!=null)image.color=Color.clear;var text=button.GetComponentInChildren<TMP_Text>(true);if(text!=null){StyleInk(text,size);text.enableAutoSizing=false;button.targetGraphic=text;}CompanionVisualFoundation.Style(button,root.VisualSettings.Value.Theme);}
        void BuildMusicHub()
        {
            musicHub=UI.Rect(canvas.transform,"Music hub",new Vector2(-415,289),new Vector2(395,108));
            var titleButton=UI.Button(musicHub,"RevealMusic","",new Vector2(24,24),new Vector2(350,42),()=>hubPinned=!hubPinned);FloatButton(titleButton,26);
            musicTitle=titleButton.GetComponentInChildren<TMP_Text>(true);musicTitle.alignment=TextAlignmentOptions.Left;musicTitle.fontStyle=FontStyles.Italic;
            if(trackFont==null&&Font.GetOSInstalledFontNames().Any(name=>name.Equals("Georgia",StringComparison.OrdinalIgnoreCase)))
            {
                var source=Font.CreateDynamicFontFromOSFont("Georgia",48);trackFont=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
                if(trackFont!=null)trackFont.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{UI.Chinese};
            }
            if(trackFont!=null)musicTitle.font=trackFont;
            var emblem=IconButton(musicHub,"Music emblem","icon-double-star",new Vector2(-188,24),ToggleMusicEmblem);emblemImage=emblem.GetComponent<Image>();
            var row=UI.Rect(musicHub,"Music controls",new Vector2(0,-17),new Vector2(395,53));musicControls=row.gameObject.AddComponent<CanvasGroup>();musicControls.alpha=0;
            IconButton(row,"Previous track","icon-prev",new Vector2(-165,0),()=>_ = HubStepAsync(-1));
            pauseImage=IconButton(row,"Pause music","icon-pause",new Vector2(-121,0),ToggleMusicPause).GetComponent<Image>();
            IconButton(row,"Next track","icon-next",new Vector2(-77,0),()=>_ = HubStepAsync(1));
            IconButton(row,"Mute music","icon-volume",new Vector2(-33,0),()=>_ = ToggleHubMuteAsync());
            AddSlider(row,"Music volume",new Vector2(78,0),0,1,value=>{music.SetVolume(value,music.Muted);hubVolumePending=true;hubSaveAt=Time.unscaledTime+.3f;});
            hubVolume=row.Find("Music volume").GetComponent<Slider>();hubVolume.GetComponent<RectTransform>().sizeDelta=new Vector2(145,4);hubVolume.handleRect.sizeDelta=new Vector2(8,16);hubVolume.SetValueWithoutNotify(root.Config.Configuration.BgmVolume);
            var trigger=hubVolume.gameObject.AddComponent<EventTrigger>();var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerUp};entry.callback.AddListener(eventData=>{_ = FlushHubVolumeAsync();});trigger.triggers.Add(entry);
        }
        Button IconButton(Transform parent,string name,string icon,Vector2 pos,UnityEngine.Events.UnityAction clicked)
        {
            var image=UI.Panel(parent,name,pos,new Vector2(30,30),CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Accent));image.sprite=CompanionVisualFoundation.Icon(icon);image.preserveAspect=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;CompanionVisualFoundation.Style(button,root.VisualSettings.Value.Theme);button.onClick.AddListener(clicked);return button;
        }
        async Task HubStepAsync(int step)
        {
            try{await StepMusicAsync(step);}catch(OperationCanceledException){}catch(Exception e){root.Logger?.Info("music hub step failed type="+e.GetType().Name);ShowFeedback("这首音乐暂时没能打开，原来的音乐会继续。");}
        }
        async Task ToggleHubMuteAsync()
        {music.SetVolume(music.Volume,!music.Muted);hubVolumePending=true;await FlushHubVolumeAsync();}
        async Task FlushHubVolumeAsync()
        {
            if(hubSaving){await hubSaveTask;if(!hubVolumePending)return;}
            if(!hubVolumePending)return;hubVolumePending=false;hubSaving=true;
            try{var copy=root.Config.Configuration.Copy();copy.BgmVolume=music.Volume;copy.BgmMuted=music.Muted;hubSaveTask=root.Config.SaveAsync(copy,root.ApplicationToken);await hubSaveTask;}
            catch(OperationCanceledException){}catch(Exception e){root.Logger?.Info("music hub save failed type="+e.GetType().Name);ShowFeedback("音量暂时没能保存，可以再试一次。");}
            finally{hubSaving=false;}
        }
        void UpdateImmersiveUI()
        {
            if(musicHub==null)return;
            if(panelName==null&&!inputOpen&&Keyboard.current!=null&&Keyboard.current.tabKey.wasPressedThisFrame)
            {
                var current=EventSystem.current?.currentSelectedGameObject;
                if(!HasSelectedControl())FocusMusicControls();
                else if(current!=null&&current.transform.IsChildOf(musicHub))
                {var items=musicHub.GetComponentsInChildren<Selectable>().Where(item=>item.IsActive()&&item.IsInteractable()).ToArray();int at=Array.FindIndex(items,item=>item.gameObject==current);if(items.Length>0)EventSystem.current.SetSelectedGameObject(items[(at+(Keyboard.current.shiftKey.isPressed?-1:1)+items.Length)%items.Length].gameObject);}
            }
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight=Screen.width/(float)Screen.height>=1280f/720?1:0;
            var selected=EventSystem.current?.currentSelectedGameObject;
            bool focused=selected!=null&&selected.transform.IsChildOf(musicHub);
            bool nearby=Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(musicHub,Mouse.current.position.ReadValue(),null);
            bool reveal=panelName==null&&!inputOpen&&(focused||nearby||hubPinned);
            musicHub.Find("RevealMusic").GetComponent<Button>().interactable=panelName==null&&!inputOpen;musicHub.Find("Music emblem").GetComponent<Button>().interactable=panelName==null&&!inputOpen;
            musicControls.alpha=Mathf.MoveTowards(musicControls.alpha,reveal?1:0,Time.unscaledDeltaTime*6);musicControls.blocksRaycasts=musicControls.interactable=reveal;
            var track=root.Library.Music.Tracks.Find(item=>item.Id==libraryMusic.CurrentId);musicTitle.text=track?.Title??"暂无音乐";
            pauseImage.sprite=CompanionVisualFoundation.Icon(music.Paused?"icon-play":"icon-pause");
            foreach(var button in musicControls.GetComponentsInChildren<Button>())button.interactable=button.name=="Pause music"?music.HasPlaylist:button.name=="Mute music"||root.Library.Music.Tracks.Count>0;
            hubVolume.interactable=true;
            if(!hubVolumePending&&!hubSaving)hubVolume.SetValueWithoutNotify(music.Volume);
            if(hubVolumePending&&!hubSaving&&Time.unscaledTime>=hubSaveAt)_=FlushHubVolumeAsync();
        }
        System.Collections.IEnumerator FadeInputAway(int revision)
        {while(inputRevision==revision&&!inputOpen&&inputFade.alpha>0){inputFade.alpha=Mathf.MoveTowards(inputFade.alpha,0,Time.unscaledDeltaTime*10);yield return null;}if(inputRevision==revision&&!inputOpen)inputPanel.gameObject.SetActive(false);}
        void BuildReadingPage(RectTransform panel,string title,int selected,ScrollRect scroll,TMP_Text note)
        {
            BuildFloatingPage(panel,title,selected,note);
            Place(scroll.GetComponent<RectTransform>(),548,339,620,267);scroll.GetComponent<Image>().color=Color.clear;scroll.GetComponent<RectMask2D>().softness=new Vector2Int(0,10);
            var layout=scroll.content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(8,16,5,28);layout.spacing=14;
        }
        void BuildFloatingPage(RectTransform panel,string title,int selected,TMP_Text note)
        {
            panel.sizeDelta=new Vector2(1280,720);panel.GetComponent<Image>().color=Color.clear;
            var heading=panel.Find("Title").GetComponent<TMP_Text>();heading.text=title;Place(heading.rectTransform,548,246,430,55);StyleInk(heading,32);heading.alignment=TextAlignmentOptions.TopLeft;
            var close=panel.Find("Close").GetComponent<Button>();Place(close.GetComponent<RectTransform>(),1095,658,95,32);FloatButton(close,15);
            Place(note.rectTransform,548,658,530,40);StyleInk(note,14,.72f);note.alignment=TextAlignmentOptions.TopLeft;
            string[] labels={"聊天记录","日记","陪伴空间","人物设定","设置","回到未花"};
            for(int i=0;i<labels.Length;i++)
            {int index=i;var button=UI.Button(panel,"Reading nav "+i,labels[i],Vector2.zero,new Vector2(185,44),()=>NavigateReading(index));Place(button.GetComponent<RectTransform>(),332,284+i*44,185,44);FloatButton(button,26);button.GetComponentInChildren<TMP_Text>(true).alignment=TextAlignmentOptions.Left;if(i==selected){button.GetComponentInChildren<TMP_Text>(true).color=Color.white;var accent=UI.Panel(panel,"Reading selection",Vector2.zero,new Vector2(3,16),CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Accent),false);Place(accent.rectTransform,322,296+i*44,3,16);}}
        }
        void NavigateReading(int index)
        {
            if(index==5){if(!TryNavigate(()=>NavigateReading(index)))return;CloseAdditionalPanels();panelName=null;menuOpen=false;if(WindowView!=null){WindowView.PanelOpen=false;WindowView.GoCharacter();}ResolveVisual();return;}
            if(index==0&&panelName=="MomoTalk"||index==1&&panelName=="Diary"||index==2&&panelName=="Spaces"||index==3&&panelName=="CharacterCard"||index==4&&(panelName=="Voice"||panelName=="Display"||panelName=="Settings"))return;
            WindowMenu(index);
        }
        public void ContinueDiaryReading()
        {
            Canvas.ForceUpdateCanvases();float overflow=diaryContent.rect.height-diaryScroll.viewport.rect.height;
            diaryScroll.verticalNormalizedPosition=overflow<=0?1:Mathf.Max(0,diaryScroll.verticalNormalizedPosition-diaryScroll.viewport.rect.height*.8f/overflow);
        }
        void OpenCurrentDiaryFile()
        {
            if(diaries.Count==0){PlayerFiles.OpenFolder(root.Diary.Folder);return;}
            try{var path=root.Diary.ExportPath(diaries[diaryIndex]);if(System.IO.File.Exists(path))PlayerFiles.ReadText(path);else diaryNote.text="这篇文件暂时不可用，可以重新打开日记检查导出。";}
            catch(Exception e){root.Logger?.Info("diary open file failed type="+e.GetType().Name);diaryNote.text="暂时没能打开文件，可以从资料文件夹查看。";}
        }
    }
}

