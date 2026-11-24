using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ResidualSignal
{
    /// <summary>
    /// Common infected from Mixamo: four zombie bodies sharing one Mixamo skeleton, driven by the Mixamo zombie
    /// packs (legacy clips). Built from InfectedWorker.prefab like the special infected, then set as the
    /// scene's common prefabs. Batch: -executeMethod ResidualSignal.StationSceneBuilder.InstallMixamoInfected
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const string MixamoRoot = "Assets/Art/Models/Mixamo";
        static readonly string[] MixamoBodies = { "Zombiegirl", "Copzombie", "Warzombie", "Parasite" };

        [MenuItem("잔류신호/일반 감염체 Mixamo 교체")]
        public static void InstallMixamoInfected()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MixamoRoot }))
                PrepareMixamoImport(AssetDatabase.GUIDToAssetPath(guid));

            AnimationClip Clip(string pack, string file) => AssetDatabase
                .LoadAllAssetsAtPath($"{MixamoRoot}/Animations/{pack}/{file}.fbx").OfType<AnimationClip>()
                .First(clip => !clip.name.StartsWith("__preview__"));
            var idle = Clip("NotSoScary", "zombie idle");
            var walk = Clip("Scary", "zombie walk");
            var run = Clip("Scary", "zombie run");
            var attack = Clip("Scary", "zombie attack");
            var hit = Clip("NotSoScary", "zombie reaction hit");

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/Prefabs/NetworkPrefabs.asset");
            var prefabs = new GameObject[MixamoBodies.Length];
            for (int index = 0; index < MixamoBodies.Length; index++)
            {
                prefabs[index] = BuildMixamoInfected(MixamoBodies[index], 1.72f + index * 0.03f, idle, walk, run, attack, hit);
                if (!list.PrefabList.Any(entry => entry.Prefab == prefabs[index]))
                    list.Add(new NetworkPrefab { Prefab = prefabs[index] });
            }
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var game = new SerializedObject(Object.FindFirstObjectByType<StationTrial>());
            var commons = game.FindProperty("commonPrefabs");
            commons.arraySize = prefabs.Length;
            for (int index = 0; index < prefabs.Length; index++)
                commons.GetArrayElementAtIndex(index).objectReferenceValue = prefabs[index].GetComponent<StationInfected>();
            game.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Installed {prefabs.Length} Mixamo infected.");
        }

        /// <summary>Evidence render: the four bodies mid-walk in the loading bay. -lineup path.png</summary>
        public static void CaptureMixamoLineup()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            string path = arguments[System.Array.IndexOf(arguments, "-lineup") + 1];
            EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(-2.4f, 1.55f, 57.2f), Quaternion.Euler(6f, 0f, 0f));
            foreach (Transform child in camera.transform)
                child.gameObject.SetActive(false);
            for (int index = 0; index < MixamoBodies.Length; index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Mixamo{MixamoBodies[index]}.prefab");
                var actor = Object.Instantiate(prefab, new Vector3(-4.2f + index * 1.2f, 0.03f, 60.6f), Quaternion.Euler(0, 180f, 0));
                var infected = new SerializedObject(actor.GetComponent<StationInfected>());
                var rig = (Animation)infected.FindProperty("rig").objectReferenceValue;
                var walk = (AnimationClip)infected.FindProperty("walkClip").objectReferenceValue;
                walk.SampleAnimation(rig.gameObject, walk.length * (0.2f + index * 0.17f));
            }
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            EditorApplication.Exit(0);
        }

        static void PrepareMixamoImport(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.animationType != ModelImporterAnimationType.Legacy;
            importer.animationType = ModelImporterAnimationType.Legacy;
            if (path.Contains("/Characters/"))
            {
                // Pull the embedded textures out so HDRP materials can reference them.
                string folder = System.IO.Path.ChangeExtension(path, null) + " textures";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(folder));
                    importer.ExtractTextures(folder);
                    changed = true;
                }
            }
            if (changed)
                importer.SaveAndReimport();
        }

        /// <summary>HDRP Lit from the extracted maps. "*body*" slots take the body diffuse when the pack has one.</summary>
        static Material MixamoSkin(string body, string slot)
        {
            string path = $"Assets/Rendering/Mixamo - {body} {slot}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            string folder = $"{MixamoRoot}/Characters/{body} textures";
            string[] maps = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string bodyDiffuse = maps.FirstOrDefault(map => map.EndsWith("_body_diffuse.png"));
            string diffuse = slot.ToLowerInvariant().Contains("body") && bodyDiffuse != null
                ? bodyDiffuse : maps.FirstOrDefault(map => map.EndsWith("_diffuse.png") && !map.EndsWith("_body_diffuse.png")) ?? bodyDiffuse;
            string normal = maps.FirstOrDefault(map => map.EndsWith("_normal.png"));

            material = new Material(Shader.Find("HDRP/Lit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            if (normal != null)
                material.SetTexture("_NormalMap", ImportNormal(normal));
            material.SetFloat("_Smoothness", 0.32f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject BuildMixamoInfected(string body, float height, AnimationClip idle, AnimationClip walk,
            AnimationClip run, AnimationClip attack, AnimationClip hit)
        {
            var actor = PrefabUtility.LoadPrefabContents("Assets/Prefabs/InfectedWorker.prefab");
            try
            {
                actor.name = "Mixamo " + body;
                var infected = new SerializedObject(actor.GetComponent<StationInfected>());
                var rigField = infected.FindProperty("rig");
                Transform oldModel = ((Animation)rigField.objectReferenceValue).transform;
                while (oldModel.parent != actor.transform)
                    oldModel = oldModel.parent;
                Object.DestroyImmediate(oldModel.gameObject);

                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{MixamoRoot}/Characters/{body}.fbx");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, actor.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // Scale by the skeleton, not mesh bounds: some Mixamo skins carry bounds far larger than the body.
                var top = model.GetComponentsInChildren<Transform>().First(bone => bone.name.EndsWith("HeadTop_End"));
                model.transform.localScale *= height / Mathf.Max(top.position.y - model.transform.position.y, 0.01f);
                if (!model.TryGetComponent(out Animation animation))
                    animation = model.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.updateWhenOffscreen = true;
                    skin.sharedMaterials = skin.sharedMaterials.Select(material => MixamoSkin(body, material.name)).ToArray();
                }
                rigField.objectReferenceValue = animation;
                infected.FindProperty("idleClip").objectReferenceValue = idle;
                infected.FindProperty("walkClip").objectReferenceValue = walk;
                infected.FindProperty("runClip").objectReferenceValue = run;
                infected.FindProperty("attackClip").objectReferenceValue = attack;
                infected.FindProperty("hitClip").objectReferenceValue = hit;
                infected.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(actor, $"Assets/Prefabs/Mixamo{body}.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }
        }
    }
}
