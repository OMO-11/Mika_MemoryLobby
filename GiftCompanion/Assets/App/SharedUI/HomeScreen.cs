using GiftCompanion.Bootstrap;
using TMPro;
using UnityEngine;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.SharedUI
{
    public sealed class HomeScreen : MonoBehaviour
    {
        private void Start()
        {
            var root = AppRoot.Instance; if (root == null) return;
            var canvas = UI.Canvas("Home Interface");
            UI.Panel(canvas.transform, "Backdrop", Vector2.zero, new Vector2(2400, 1600), new Color(.055f, .07f, .12f), false);
            UI.Panel(canvas.transform, "Accent", new Vector2(0, 170), new Vector2(42, 3), UI.Accent, false);
            UI.Text(canvas.transform, "CharacterName", root.Character.Identity.DisplayName, new Vector2(0, 95), new Vector2(800, 80), 44);
            UI.Text(canvas.transform, "Invitation", "老师，回到我们的陪伴空间吧。", new Vector2(0, 20), new Vector2(850, 50), 25);
            var navigation = GetComponent<ModeSceneController>();
            UI.Button(canvas.transform, "Companion", "陪伴空间", new Vector2(root.EntrySettings.PetEntryEnabled ? -160 : 0, -75), new Vector2(280, 64), navigation.OpenCompanion);
            if (root.EntrySettings.PetEntryEnabled) UI.Button(canvas.transform, "AgentPet", "来到桌面", new Vector2(160, -75), new Vector2(280, 64), navigation.OpenAgentPet);
            UI.Button(canvas.transform, "Maintenance", "资料与连接", new Vector2(0, -150), new Vector2(280, 46), PlayerMaintenance.Open);
            UI.Button(canvas.transform, "Quit", "退出", new Vector2(0, -215), new Vector2(120, 42), navigation.Quit);
            if (root.EntrySettings.PetEntryEnabled) UI.Button(canvas.transform, "ResetPet", "找回桌宠位置", new Vector2(0, -275), new Vector2(210, 36), () =>
            {
                var settings = root.PetSettings.Value.Copy(); settings.HasPosition = false; root.PetSettings.Save(settings, root.ApplicationToken);
            });
            if (PlayerMaintenance.RequestedFromMode || !string.IsNullOrEmpty(root.RestoreNotice) || !root.LLM.IsConfigured && !System.IO.File.Exists(System.IO.Path.Combine(root.Config.UserDataPath, "Config", "onboarding.json"))) PlayerMaintenance.Open();
        }
    }
}
