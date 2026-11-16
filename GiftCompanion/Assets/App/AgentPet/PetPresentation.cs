using System;
using GiftCompanion.Bootstrap;
using UnityEngine;

namespace GiftCompanion.AgentPet
{
    // Scene-local visual adapter. Shared character state and services remain owned by AppRoot.
    public sealed class PetPresentation : MonoBehaviour
    {
        [SerializeField] private CHDDDBodyControllerV2 body;
        private static readonly string[] allowedActions =
            { "Idle", "Walk", "Thinking", "Working", "HeadPat", "Dragging", "Sleep", "Wave", "Success", "Confused", "HeadClick", "BodyClick" };
        private static readonly string[] allowedEmotions =
            { "neutral", "happy", "shy", "concern", "sad", "surprised", "annoyed", "sleepy" };
        public CHDDDBodyControllerV2 Body => body;

        private void Start()
        {
            if (body == null) body = GetComponentInChildren<CHDDDBodyControllerV2>();
            if (body == null) throw new InvalidOperationException("Pet body binding is missing.");
            body.ResumeActionExpressions(0);
            body.PlayAction("Idle", 0);
            AppRoot.Instance?.Logger?.Info("pet presentation ready delivery=7");
        }

        public bool PlayAction(string action)
        {
            if (body == null) return false;
            return body.PlayAction(Array.IndexOf(allowedActions, action) >= 0 ? action : "Idle");
        }

        public void SetEmotion(string emotion)
        {
            if (body != null) body.SetManualEmotion(Array.IndexOf(allowedEmotions, emotion) >= 0 ? emotion : "neutral");
        }

        public void ResumeActionFace() => body?.ResumeActionExpressions();
        public void ReleaseInteraction() => body?.ReturnToIdle();
    }
}
