using UnityEngine;

namespace Interactive360
{
    /// <summary>
    /// Drives a Skybox/Panoramic material with whatever texture the current video slot renders into.
    /// Supports mono, over-under and side-by-side stereo equirectangular layouts.
    /// </summary>
    public sealed class PanoramaSkybox : MonoBehaviour
    {
        [Tooltip("Material using the Skybox/Panoramic shader. Falls back to the scene skybox, then creates one.")]
        public Material template;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int LayoutId = Shader.PropertyToID("_Layout");
        static readonly int MappingId = Shader.PropertyToID("_Mapping");
        static readonly int ImageTypeId = Shader.PropertyToID("_ImageType");
        static readonly int MirrorId = Shader.PropertyToID("_MirrorOnBack");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int RotationId = Shader.PropertyToID("_Rotation");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        Material material;
        public Material Material => material;

        void Awake()
        {
            var source = template != null ? template : RenderSettings.skybox;
            if (source == null || source.shader == null || source.shader.name != "Skybox/Panoramic")
            {
                var shader = Shader.Find("Skybox/Panoramic");
                if (shader == null) { Debug.LogError("[PanoramaSkybox] Skybox/Panoramic shader not found."); return; }
                source = new Material(shader);
            }
            material = new Material(source) { name = "Panorama (runtime)" };
            material.SetFloat(MappingId, 1f);      // latitude-longitude
            material.SetFloat(ImageTypeId, 0f);    // 360 degrees
            material.SetFloat(MirrorId, 0f);
            material.SetFloat(ExposureId, 1f);
            material.SetFloat(RotationId, 0f);
            material.SetColor(TintId, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            RenderSettings.skybox = material;
            SetTexture(null, "mono");
        }

        public void SetTexture(Texture texture, string layout)
        {
            if (material == null) return;
            material.SetTexture(MainTexId, texture != null ? texture : Texture2D.blackTexture);
            material.SetFloat(LayoutId, LayoutCode(layout));
        }

        public static float LayoutCode(string layout)
        {
            switch ((layout ?? "mono").Trim().ToLowerInvariant())
            {
                case "side-by-side": case "sbs": case "lr": return 1f;
                case "over-under": case "ou": case "tb": return 2f;
                default: return 0f;
            }
        }

        public void SetExposure(float exposure)
        {
            if (material != null) material.SetFloat(ExposureId, Mathf.Max(0f, exposure));
        }

        public void SetRotation(float degrees)
        {
            if (material != null) material.SetFloat(RotationId, Mathf.Repeat(degrees, 360f));
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
