using System;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiftCompanion.SharedUI
{
    // Scene-local navigation only. This component never constructs shared services.
    public sealed class ModeSceneController : MonoBehaviour
    {
        [SerializeField] private CharacterMode sceneMode;
        [SerializeField] private Button[] navigationButtons;
        [SerializeField] private TMPro.TMP_Text feedback;
        public CharacterMode SceneMode => sceneMode;

        private void Start()
        {
            // Opening an individual scene in the editor always returns through Bootstrap.
            if (AppRoot.Instance == null)
                SceneManager.LoadScene("Assets/Scenes/Bootstrap.unity");
            else if (sceneMode == CharacterMode.Companion && GetComponent<Companion.CompanionScreen>() == null)
                gameObject.AddComponent<Companion.CompanionScreen>();
            else if (sceneMode == CharacterMode.Home && GetComponent<HomeScreen>() == null)
                gameObject.AddComponent<HomeScreen>();
            else if (sceneMode == CharacterMode.AgentPet && GetComponent<AgentPet.PetScreen>() == null)
                gameObject.AddComponent<AgentPet.PetScreen>();
        }

        public void OpenCompanion() => Navigate(CharacterMode.Companion);
        public void OpenAgentPet() => Navigate(CharacterMode.AgentPet);
        public void ReturnHome() => Navigate(CharacterMode.Home);

        private async void Navigate(CharacterMode mode)
        {
            var root = AppRoot.Instance;
            if (root == null || !root.IsReady || root.Controller.IsSwitching) return;
            foreach (var button in navigationButtons) if (button != null) button.interactable = false;
            try { await root.Controller.SwitchModeAsync(mode, destroyCancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (feedback != null) feedback.text = "Please try again.";
            }
            finally
            {
                if (this != null)
                    foreach (var button in navigationButtons) if (button != null) button.interactable = true;
            }
        }

        public void Quit()
        {
            AppRoot.Instance?.RequestQuit();
        }
    }
}
