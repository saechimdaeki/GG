using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>Photographic night look: volumetric haze, AO, grading, moonlight and practical light spill.</summary>
    public static partial class StationSceneBuilder
    {
        /// <summary>Batch entry: re-grade the global volume only, without rebuilding or touching the scene.</summary>
        public static void ApplyLook()
        {
            var global = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{SettingsFolder}/StationVolume.asset");
            ApplyCinematicLook(global);
            EditorUtility.SetDirty(global);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Batch entry, run after Build: recolor the green-white fluorescents in the saved scene to warm white,
        /// every third to amber, so interiors get warm pools against the cold grade. Idempotent: warm lights no longer match.</summary>
        public static void WarmInteriorLights()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var lamps = new System.Collections.Generic.List<Light>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                Color c = light.color;
                // ponytail: "greenish" = green channel leads; exterior moonlight/street blue (b > g) stays cold.
                if (light.type != LightType.Directional && c.g >= c.r && c.g >= c.b &&
                    light.GetComponentInParent<StationTrial>() == null)
                    lamps.Add(light);
            }
            lamps.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            for (int index = 0; index < lamps.Count; index++)
            {
                lamps[index].color = index % 3 == 2 ? new Color(1f, 0.66f, 0.38f) : new Color(1f, 0.9f, 0.76f);
                EditorUtility.SetDirty(lamps[index]);
            }
            Debug.Log($"Warmed {lamps.Count} interior lights.");
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        static void ApplyCinematicLook(VolumeProfile profile)
        {
            var fog = StationVolumeSetting<Fog>(profile);
            fog.enabled.Override(true);
            fog.meanFreePath.Override(105f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(14f);
            fog.albedo.Override(new Color(0.63f, 0.74f, 0.81f));
            fog.enableVolumetricFog.Override(true);
            fog.colorMode.Override(FogColorMode.SkyColor);
            fog.mipFogMaxMip.Override(0.6f);
            fog.depthExtent.Override(48f);
            EditorUtility.SetDirty(fog);

            var bloom = StationVolumeSetting<Bloom>(profile);
            bloom.intensity.Override(0.24f);
            bloom.scatter.Override(0.78f);
            bloom.threshold.Override(0.9f);
            EditorUtility.SetDirty(bloom);

            var ao = StationVolumeSetting<ScreenSpaceAmbientOcclusion>(profile);
            ao.intensity.Override(1.6f);
            ao.radius.Override(1.4f);
            ao.directLightingStrength.Override(0.25f);
            EditorUtility.SetDirty(ao);

            var contact = StationVolumeSetting<ContactShadows>(profile);
            contact.enable.Override(true);
            contact.length.Override(0.18f);
            EditorUtility.SetDirty(contact);

            // Warm practicals against cold shadows. The tint pulls the fluorescent green cast toward neutral.
            var balance = StationVolumeSetting<WhiteBalance>(profile);
            balance.temperature.Override(9f);
            balance.tint.Override(9f);
            EditorUtility.SetDirty(balance);

            var split = StationVolumeSetting<SplitToning>(profile);
            split.shadows.Override(new Color(0.34f, 0.44f, 0.64f));
            split.highlights.Override(new Color(0.78f, 0.56f, 0.34f));
            split.balance.Override(-8f);
            EditorUtility.SetDirty(split);

            // Lift shadows toward the reference's mid-grey night (median ~48/255) without clipping the signs.
            var lgg = StationVolumeSetting<LiftGammaGain>(profile);
            lgg.lift.Override(new Vector4(1f, 1f, 1f, ShadowLift));
            lgg.gamma.Override(new Vector4(1f, 1f, 1f, ShadowGamma));
            EditorUtility.SetDirty(lgg);
            var adjust = StationVolumeSetting<ColorAdjustments>(profile);
            adjust.postExposure.Override(PostExposure);
            adjust.contrast.Override(22f);
            adjust.saturation.Override(2f);
            adjust.colorFilter.Override(new Color(1f, 0.985f, 0.97f));
            EditorUtility.SetDirty(adjust);

            var vignette = StationVolumeSetting<Vignette>(profile);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(vignette);

            var grain = StationVolumeSetting<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.10f);
            grain.response.Override(0.85f);
            EditorUtility.SetDirty(grain);

            var lens = StationVolumeSetting<ChromaticAberration>(profile);
            lens.intensity.Override(0.025f);
            EditorUtility.SetDirty(lens);
        }

        /// <summary>Inside the station the night sky can't light anything; a local volume swaps in a dim
        /// gradient ambient so fluorescent bounce reads instead of pitch black.</summary>
        static void AddInteriorAmbience()
        {
            const string path = SettingsFolder + "/StationInteriorVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            var environment = StationVolumeSetting<VisualEnvironment>(profile);
            environment.skyType.Override((int)SkyType.Gradient);
            EditorUtility.SetDirty(environment);
            var sky = StationVolumeSetting<GradientSky>(profile);
            sky.top.Override(new Color(0.55f, 0.58f, 0.54f));
            sky.middle.Override(new Color(0.44f, 0.45f, 0.41f));
            sky.bottom.Override(new Color(0.20f, 0.20f, 0.19f));
            sky.multiplier.Override(1f);
            sky.exposure.Override(StationInteriorSkyExposure);
            EditorUtility.SetDirty(sky);
            var fog = StationVolumeSetting<Fog>(profile);
            fog.meanFreePath.Override(30f);
            fog.albedo.Override(new Color(0.70f, 0.72f, 0.66f));
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.color.Override(new Color(0.05f, 0.055f, 0.05f));
            EditorUtility.SetDirty(fog);

            var node = new GameObject("Station interior ambience", typeof(BoxCollider), typeof(Volume));
            var box = node.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 2.2f, 18.5f);
            box.size = new Vector3(16f, 6f, 37f);
            var volume = node.GetComponent<Volume>();
            volume.isGlobal = false;
            volume.blendDistance = 3.5f;
            volume.priority = 1;
            volume.sharedProfile = profile;
        }

        // Brightness knobs: raise these if the night reads too dark on a given display.
        const float PostExposure = 0.85f;
        const float ShadowGamma = 0.1f;
        const float ShadowLift = 0.015f;
        const float StationInteriorSkyExposure = 6.6f;

        static void AddMoonlight()
        {
            var moon = new GameObject("Moonlight behind station");
            moon.transform.rotation = Quaternion.Euler(24f, 172f, 0f);
            moon.AddHDLight(LightType.Directional);
            var light = moon.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.color = new Color(0.62f, 0.74f, 0.92f);
            light.intensity = 420f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>Warm facade wash and coloured spill from the overhead festoon.</summary>
        static void AddForecourtPractical(Transform plaza)
        {
            Spotlight(plaza, "Facade wash left", new(-5.2f, 0.4f, -8.4f), new(-3.8f, 4.4f, -3.0f),
                new Color(1f, 0.72f, 0.45f), 16000, 58f, 14f);
            Spotlight(plaza, "Facade wash right", new(5.2f, 0.4f, -8.4f), new(3.8f, 4.4f, -3.0f),
                new Color(1f, 0.72f, 0.45f), 16000, 58f, 14f);
            Spotlight(plaza, "Sign underlight", new(0, 2.9f, -8.6f), new(0, 4.4f, -7.6f),
                new Color(1f, 0.84f, 0.62f), 2600, 110f, 6f);
            Color[] tints = { new(1f, 0.38f, 0.22f), new(1f, 0.78f, 0.46f), new(0.56f, 0.92f, 0.46f) };
            for (int strand = 0; strand < 2; strand++)
            for (int index = 2; index < 18; index += 4)
            {
                var light = StationLight(plaza, FestoonPoint(index / 18f, -9.10f - strand * 4.8f, strand) - Vector3.up * 0.1f,
                    tints[(index / 4 + strand) % 3], 1400, 5.5f);
                light.shadows = LightShadows.None;
            }
        }
    }
}
