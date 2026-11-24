using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>Network manager, session menu and the networked prefabs (rescuer avatar, mission mirror, infected).</summary>
    public static partial class StationSceneBuilder
    {
        const string RescuerFolder = "Assets/Art/Models/Rescuer/";

        static void CreateNetwork(SerializedObject game)
        {
            var avatar = RescuerPrefab(out var bot);
            game.FindProperty("botPrefab").objectReferenceValue = bot;
            var syncSource = new GameObject("Mission sync", typeof(NetworkObject), typeof(MissionSync));
            var sync = PrefabUtility.SaveAsPrefabAsset(syncSource, "Assets/Prefabs/MissionSync.prefab");
            Object.DestroyImmediate(syncSource);
            var infected = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/InfectedWorker.prefab");

            const string listPath = "Assets/Prefabs/NetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(listPath);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            list.Add(new NetworkPrefab { Prefab = infected });
            list.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LoadingYardMutant.prefab") });
            list.Add(new NetworkPrefab { Prefab = sync });
            list.Add(new NetworkPrefab { Prefab = avatar });
            foreach (var path in Directory.GetFiles("Assets/Prefabs", "InfectedCivilian*.prefab"))
                list.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) });
            foreach (string kind in new[] { "Leaper", "Dragger", "Bloater", "Caller" })
            {
                var alternate = Resources.Load<GameObject>($"ResidualSignal/Infected/{kind}");
                if (alternate != null)
                    list.Add(new NetworkPrefab { Prefab = alternate });
            }
            AssetDatabase.CreateAsset(list, listPath);

            var node = new GameObject("Network", typeof(NetworkManager), typeof(UnityTransport), typeof(SessionMenu));
            var manager = node.GetComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = node.GetComponent<UnityTransport>(),
                PlayerPrefab = avatar,
                EnableSceneManagement = false,
            };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            var menu = new SerializedObject(node.GetComponent<SessionMenu>());
            menu.FindProperty("missionSyncPrefab").objectReferenceValue = sync.GetComponent<NetworkObject>();
            menu.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Rocketbox firefighter (MIT) as the body other survivors see.</summary>
        static GameObject RescuerPrefab(out SurvivorBot botPrefab)
        {
            var body = RocketboxMaterial("Rescuer - turnout coat", "body_color.jpg", "body_normal.png", false);
            var head = RocketboxMaterial("Rescuer - face", "head_color.jpg", "head_normal.png", false);
            var helmet = RocketboxMaterial("Rescuer - helmet", "helmet_color.jpg", "helmet_normal.png", false);
            var visor = RocketboxMaterial("Rescuer - visor", "opacity_color.png", "opacity_normal.png", false);
            visor.SetFloat("_SurfaceType", 1f);
            visor.SetColor("_BaseColor", new Color(0.42f, 0.46f, 0.42f, 0.20f));
            HDMaterial.ValidateMaterial(visor);
            EditorUtility.SetDirty(visor);
            var idle = ImportLegacyClip(RescuerFolder + "idle.fbx");
            var run = ImportLegacyClip(RescuerFolder + "run.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(RescuerFolder + "Rescuer.fbx");
            if (importer.animationType != ModelImporterAnimationType.Legacy)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.SaveAndReimport();
            }

            var root = new GameObject("Rescuer");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RescuerFolder + "Rescuer.fbx"), root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                {
                    string slot = slots[index] != null ? slots[index].name.ToLowerInvariant() : "";
                    slots[index] = slot.Contains("opacity") ? visor : slot.Contains("helmet") ? helmet :
                        slot.Contains("head") ? head : body;
                }
                renderer.sharedMaterials = slots;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }
            FitModel(model, 1.8f);
            var player = model.GetComponent<Animation>();
            if (player == null)
                player = model.AddComponent<Animation>();
            player.playAutomatically = false;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            var walk = ImportLegacyClip(RescuerFolder + "walk.fbx");
            AddRescuerKit(root.transform);
            botPrefab = RescuerBot(root, player, idle, walk, run);
            root.AddComponent<NetworkObject>();
            var survivor = new SerializedObject(root.AddComponent<SurvivorAvatar>());
            survivor.FindProperty("rig").objectReferenceValue = player;
            survivor.FindProperty("idleClip").objectReferenceValue = idle;
            survivor.FindProperty("runClip").objectReferenceValue = run;
            survivor.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<NetworkTransform>().AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            Directory.CreateDirectory("Assets/Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Rescuer.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Saves a solo AI teammate from the posed rescuer body: rifle at the shoulder, a voice and a muzzle light.</summary>
        static SurvivorBot RescuerBot(GameObject source, Animation rig, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            var copy = Object.Instantiate(source);
            copy.name = "Rescuer bot";
            var agent = copy.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.3f;
            // Keep physical contact with infected; allied pairs are ignored at runtime.
            var bump = copy.AddComponent<CapsuleCollider>();
            bump.height = 1.8f;
            bump.radius = 0.28f;
            bump.center = new Vector3(0, 0.9f, 0);
            var rifle = Pivot(copy.transform, "Bot rifle", new(0.17f, 1.38f, 0.28f));
            var spec = Primaries[1];
            InstallLongGun(rifle, spec, GunMaterial(spec), out Vector3 muzzlePoint, out float top, out float rear);
            var stock = Pivot(rifle, "Stock contacts shoulder", new(0f, top - 0.064f, rear + 0.021f));
            var grip = Pivot(rifle, "Trigger hand contact", new(0.012f, top - 0.146f, rear + spec.length * 0.30f));
            var support = Pivot(rifle, "Support hand under fore-end", new(-0.016f, top - 0.083f, rear + spec.length * 0.48f));
            var voice = copy.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.minDistance = 3f;
            voice.maxDistance = 40f;
            var flash = new GameObject("Bot muzzle light").AddComponent<Light>();
            flash.transform.SetParent(rifle, false);
            flash.transform.localPosition = muzzlePoint;
            flash.color = new Color(1f, 0.6f, 0.25f);
            flash.range = 6f;
            flash.intensity = 120f;
            flash.enabled = false;
            var bot = new SerializedObject(copy.AddComponent<SurvivorBot>());
            bot.FindProperty("rig").objectReferenceValue = copy.GetComponentInChildren<Animation>();
            bot.FindProperty("idleClip").objectReferenceValue = idle;
            bot.FindProperty("walkClip").objectReferenceValue = walk;
            bot.FindProperty("runClip").objectReferenceValue = run;
            bot.FindProperty("voice").objectReferenceValue = voice;
            bot.FindProperty("shot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Gun_Rifle.wav");
            bot.FindProperty("muzzle").objectReferenceValue = flash;
            bot.FindProperty("rifle").objectReferenceValue = rifle;
            bot.FindProperty("stock").objectReferenceValue = stock;
            bot.FindProperty("triggerGrip").objectReferenceValue = grip;
            bot.FindProperty("supportGrip").objectReferenceValue = support;
            var coats = bot.FindProperty("alternateCoats");
            var helmets = bot.FindProperty("alternateHelmets");
            coats.arraySize = helmets.arraySize = 2;
            string[] palettes = { "navy", "rust" };
            for (int i = 0; i < palettes.Length; i++)
            {
                string palette = palettes[i];
                coats.GetArrayElementAtIndex(i).objectReferenceValue = RocketboxMaterial(
                    $"Rescuer - {palette} coat", $"body_{palette}.jpg", "body_normal.png", false);
                helmets.GetArrayElementAtIndex(i).objectReferenceValue = RocketboxMaterial(
                    $"Rescuer - {palette} helmet", $"helmet_{palette}.jpg", "helmet_normal.png", false);
            }
            bot.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, "Assets/Prefabs/RescuerBot.prefab");
            Object.DestroyImmediate(copy);
            return prefab.GetComponent<SurvivorBot>();
        }

        static Material RocketboxMaterial(string name, string color, string normal, bool cutout)
        {
            var material = Surface(name, Color.white, 0.3f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RescuerFolder + color));
            material.SetTexture("_NormalMap", ImportNormal(RescuerFolder + normal));
            material.SetFloat("_AlphaCutoffEnable", cutout ? 1f : 0f);
            material.SetFloat("_AlphaCutoff", 0.35f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
