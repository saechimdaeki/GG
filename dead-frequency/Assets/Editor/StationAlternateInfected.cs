using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string AlternateResources = "Assets/Resources/ResidualSignal/Infected";

        [MenuItem("잔류신호/특수 감염체 모델 교체")]
        public static void InstallAlternateInfected()
        {
            System.IO.Directory.CreateDirectory(AlternateResources);
            AssetDatabase.Refresh();
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/Prefabs/NetworkPrefabs.asset");
            foreach (var (source, name, height) in new[] { ("runner", "Leaper", 1.74f), ("bloated", "Bloater", 1.75f),
                ("rotten", "Dragger", 1.83f), ("office-worker", "Caller", 1.78f) })
            {
                var actor = BuildAlternateInfected(source, name, height);
                if (!list.PrefabList.Any(entry => entry.Prefab == actor))
                    list.Add(new NetworkPrefab { Prefab = actor });
            }
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        static GameObject BuildAlternateInfected(string source, string name, float height)
        {
            string path = $"Assets/Art/Models/AlternateInfected/{source}.glb";
            var importer = AssetImporter.GetAtPath(path);
            var options = new SerializedObject(importer);
            var method = options.FindProperty("importSettings.animationMethod");
            int legacy = System.Array.IndexOf(method.enumNames, "Legacy");
            if (method.enumValueIndex != legacy)
            {
                method.enumValueIndex = legacy;
                options.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var actor = PrefabUtility.LoadPrefabContents("Assets/Prefabs/InfectedWorker.prefab");
            try
            {
                actor.name = name;
                var infected = new SerializedObject(actor.GetComponent<StationInfected>());
                var rigField = infected.FindProperty("rig");
                var oldRig = (Animation)rigField.objectReferenceValue;
                Transform oldModel = oldRig.transform;
                while (oldModel.parent != actor.transform)
                    oldModel = oldModel.parent;
                Object.DestroyImmediate(oldModel.gameObject);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), actor.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                FitModel(model, height);
                var animation = model.GetComponentInChildren<Animation>(true);
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (var body in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    body.updateWhenOffscreen = true;
                rigField.objectReferenceValue = animation;
                infected.FindProperty("idleClip").objectReferenceValue = clips["Idle"];
                infected.FindProperty("walkClip").objectReferenceValue = clips["Walk"];
                infected.FindProperty("runClip").objectReferenceValue = clips["Run"];
                infected.FindProperty("attackClip").objectReferenceValue = clips["Attack"];
                infected.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(actor, $"{AlternateResources}/{name}.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }
        }
    }
}
