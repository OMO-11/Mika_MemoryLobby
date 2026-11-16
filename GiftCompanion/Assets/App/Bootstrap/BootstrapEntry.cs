using System;
using GiftCompanion.Core.Character;
using UnityEngine;

namespace GiftCompanion.Bootstrap
{
    public sealed class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] private AppRoot appRootPrefab;

        private async void Start()
        {
            try
            {
                var root = AppRoot.Instance;
                if (root == null) root = Instantiate(appRootPrefab);
                root.gameObject.name = "AppRoot";
                await root.Initialization;
                if (root.IsReady) await root.Controller.SwitchModeAsync(root.EntrySettings.InitialMode, destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* AppRoot owns the brief failure UI and sanitized log. */ }
        }
    }
}
