using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string WitnessFolder = "Assets/Art/Models/SeoIgyeong/";

        /// <summary>Rigged Rocketbox worker (MIT) as Seo I-gyeong, with nervous idle and injured run clips.</summary>
        static Animation InstallWitnessAvatar(Transform witness)
        {
            var body = WitnessMaterial("Witness - work coverall", "body");
            var head = WitnessMaterial("Witness - face and hair", "head");
            var hidden = Surface("Witness - hidden helmet", new Color(1, 1, 1, 0), 0f);
            hidden.SetFloat("_AlphaCutoffEnable", 1f);
            hidden.SetFloat("_AlphaCutoff", 1f);
            HDMaterial.ValidateMaterial(hidden);

            var idle = ImportLegacyClip(WitnessFolder + "idle_nervous.fbx");
            var run = ImportLegacyClip(WitnessFolder + "run_injured.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(WitnessFolder + "Seo.fbx");
            if (importer.animationType != ModelImporterAnimationType.Legacy)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.SaveAndReimport();
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(WitnessFolder + "Seo.fbx"), witness);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                {
                    string slot = slots[index] != null ? slots[index].name.ToLowerInvariant() : "";
                    slots[index] = slot.Contains("helmet") ? hidden : slot.Contains("head") ? head : body;
                }
                renderer.sharedMaterials = slots;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }
            FitModel(model, 1.64f);

            var player = model.GetComponent<Animation>();
            if (player == null)
                player = model.AddComponent<Animation>();
            player.AddClip(idle, "idle");
            player.AddClip(run, "walk");
            player.clip = idle;
            player.wrapMode = WrapMode.Loop;
            player.playAutomatically = true;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            return player;
        }

        static Material WitnessMaterial(string name, string part)
        {
            var material = Surface(name, Color.white, 0.32f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WitnessFolder + part + "_color.jpg"));
            material.SetTexture("_NormalMap", ImportNormal(WitnessFolder + part + "_normal.png"));
            material.SetFloat("_NormalScale", 1f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
