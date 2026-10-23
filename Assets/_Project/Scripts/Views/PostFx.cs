using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OSistema.Views
{
    /// <summary>
    /// The film look: muted, warm colours, a gentle vignette, a little grain, and bloom so street lamps
    /// glow at night. Turns flat procedural colours into something closer to a photograph.
    /// </summary>
    public static class PostFx
    {
        public static void Setup(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            // Domain reload is off: a volume from the last Play session may still be around. Rebuild it.
            foreach (var old in Object.FindObjectsByType<Volume>())
                if (old.name == "Film look") Object.Destroy(old.gameObject);

            var go = new GameObject("Film look");
            Object.DontDestroyOnLoad(go);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-22f);
            color.contrast.Override(14f);
            color.postExposure.Override(0.2f);

            var balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(9f);   // Naples sun: warm
            balance.tint.Override(2f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.95f, 1.05f, -0.04f));   // cool, slightly deeper shadows
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.94f, 0f));     // warm highlights

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.27f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.85f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
        }
    }
}
