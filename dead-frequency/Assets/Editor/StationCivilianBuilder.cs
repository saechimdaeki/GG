using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>Ordinary infected built from Rocketbox civilians (MIT) with zombified textures and drunk/sprint motion.</summary>
    public static partial class StationSceneBuilder
    {
        const string CivilianFolder = "Assets/Art/Models/Civilians/";

        static AudioClip[] Clips(string prefix) => Directory.GetFiles("Assets/Art/Audio", prefix + "*.wav")
            .OrderBy(path => path).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

        static void SetClips(SerializedObject data, string property, AudioClip[] clips)
        {
            var list = data.FindProperty(property);
            list.arraySize = clips.Length;
            for (int index = 0; index < clips.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = clips[index];
        }

        static StationInfected[] CivilianPrefabs()
        {
            var shamble = ImportLegacyClip(CivilianFolder + "walk_drunk.fbx");
            var idle = ImportLegacyClip(CivilianFolder + "idle_drunk.fbx");
            var sprint = ImportLegacyClip(CivilianFolder + "run_fast.fbx");
            var lurch = ImportLegacyClip(CivilianFolder + "run_injured.fbx");
            var groans = Clips("Zombie_Groan");
            var screams = Clips("Zombie_Scream");
            var names = Directory.GetDirectories(CivilianFolder).Select(Path.GetFileName).OrderBy(name => name).ToArray();
            var prefabs = new StationInfected[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                string folder = CivilianFolder + names[index] + "/";
                var body = CivilianMaterial("Infected civilian - " + names[index] + " body", folder + "body.jpg", folder + "body_n.png", false);
                var head = CivilianMaterial("Infected civilian - " + names[index] + " head", folder + "head.jpg", folder + "head_n.png", false);
                var hair = File.Exists(folder + "hair.png")
                    ? CivilianMaterial("Infected civilian - " + names[index] + " hair", folder + "hair.png", null, true) : head;
                var importer = (ModelImporter)AssetImporter.GetAtPath(folder + "model.fbx");
                if (importer.animationType != ModelImporterAnimationType.Legacy)
                {
                    importer.animationType = ModelImporterAnimationType.Legacy;
                    importer.SaveAndReimport();
                }

                var actor = new GameObject("Infected " + names[index]);
                var controller = actor.AddComponent<CharacterController>();
                controller.height = 1.78f;
                controller.radius = 0.32f;
                controller.center = new Vector3(0, 0.89f, 0);
                controller.stepOffset = 0.25f;
                actor.AddComponent<Unity.Netcode.NetworkObject>();
                var infected = actor.AddComponent<StationInfected>();
                actor.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                var voice = actor.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 2f;
                voice.maxDistance = 22f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "model.fbx"), actor.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        string name = slots[slot] != null ? slots[slot].name.ToLowerInvariant() : "";
                        slots[slot] = name.Contains("opacity") ? hair : name.Contains("head") ? head : body;
                    }
                    renderer.sharedMaterials = slots;
                    if (renderer is SkinnedMeshRenderer skinned)
                        skinned.updateWhenOffscreen = true;
                }
                FitModel(model, 1.76f);
                var animation = model.GetComponent<Animation>();
                if (animation == null)
                    animation = model.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;

                var data = new SerializedObject(infected);
                data.FindProperty("rig").objectReferenceValue = animation;
                data.FindProperty("warningAudio").objectReferenceValue = voice;
                data.FindProperty("attackCue").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/InfectedWarning.wav");
                data.FindProperty("walkClip").objectReferenceValue = shamble;
                data.FindProperty("idleClip").objectReferenceValue = idle;
                data.FindProperty("runClip").objectReferenceValue = index % 3 == 2 ? lurch : sprint;
                data.FindProperty("attackClip").objectReferenceValue = null;
                SetClips(data, "groans", groans);
                SetClips(data, "screams", screams);
                data.ApplyModifiedPropertiesWithoutUndo();
                var prefab = PrefabUtility.SaveAsPrefabAsset(actor, $"Assets/Prefabs/InfectedCivilian{index}.prefab");
                Object.DestroyImmediate(actor);
                prefabs[index] = prefab.GetComponent<StationInfected>();
            }
            return prefabs;
        }

        static Material CivilianMaterial(string name, string color, string normal, bool cutout)
        {
            var material = Surface(name, Color.white, 0.22f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
            material.SetTexture("_NormalMap", normal != null ? ImportNormal(normal) : null);
            material.SetFloat("_AlphaCutoffEnable", cutout ? 1f : 0f);
            material.SetFloat("_AlphaCutoff", 0.4f);
            material.SetFloat("_DoubleSidedEnable", cutout ? 1f : 0f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
