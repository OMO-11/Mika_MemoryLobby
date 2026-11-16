using UnityEngine;

namespace GiftCompanion.Companion
{
    // Scene-only adjustment, after the complete Spine drawing and before overlay UI.
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class CompanionTone : MonoBehaviour
    {
        [Range(.75f, 1.1f)] public float exposure = .95f;
        [Range(0, 1)] public float highlightCompression = .35f;
        private Material material;
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                var shader = Shader.Find("Hidden/GiftCompanion/CompanionTone");
                if (shader == null) { Graphics.Blit(source, destination); return; }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            material.SetFloat("_Exposure", exposure);
            material.SetFloat("_Compression", highlightCompression);
            Graphics.Blit(source, destination, material);
        }
        private void OnDisable()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }
    }
}
