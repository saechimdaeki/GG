using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Builds the three squad bots from Mixamo characters with the Pro Rifle Pack motions. Meshes are copied into
    /// .asset files and textures extracted, so the (large, non-redistributable) source FBX can leave the project
    /// once this has run. Wires the models and rifle clips into RescuerBot.prefab.
    /// Batch: -executeMethod ResidualSignal.StationSceneBuilder.InstallMixamoSurvivors
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const string SurvivorRoot = MixamoRoot + "/Survivors";
        // Squad slot order: 민호, 수아, 태식.
        static readonly (string body, float height)[] SurvivorBodies = { ("Leonard", 1.78f), ("Megan", 1.66f), ("Swat", 1.82f) };

        [MenuItem("잔류신호/동료 Mixamo 교체")]
        public static void InstallMixamoSurvivors()
        {
            foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { MixamoRoot + "/Animations/Rifle" }).Select(AssetDatabase.GUIDToAssetPath))
                PrepareMixamoImport(path);
            var models = new GameObject[SurvivorBodies.Length];
            for (int i = 0; i < SurvivorBodies.Length; i++)
                models[i] = BuildSurvivorModel(SurvivorBodies[i].body, SurvivorBodies[i].height);

            var bot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/RescuerBot.prefab");
            try
            {
                var settings = new SerializedObject(bot.GetComponent<SurvivorBot>());
                var array = settings.FindProperty("survivorModels");
                array.arraySize = models.Length;
                for (int i = 0; i < models.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
                settings.FindProperty("rifleIdle").objectReferenceValue = RifleClip("idle");
                settings.FindProperty("rifleWalk").objectReferenceValue = RifleClip("walk forward");
                settings.FindProperty("rifleRun").objectReferenceValue = RifleClip("run forward");
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(bot, "Assets/Prefabs/RescuerBot.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(bot);
            }
            // The bot AK's orange varnish read as a toy: use the dark walnut recolour of the same albedo.
            var rifle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rendering/Weapon - rifle.mat");
            var walnut = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Models/Rifle/AK_1_Albedo walnut.png");
            if (rifle != null && walnut != null)
            {
                rifle.SetTexture("_BaseColorMap", walnut);
                EditorUtility.SetDirty(rifle);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Mixamo survivors installed: " + string.Join(", ", models.Select(m => m.name)));
        }

        static GameObject BuildSurvivorModel(string body, float height) =>
            BuildMixamoModel(SurvivorRoot, body, height, $"Assets/Prefabs/Survivor{body}.prefab", "Survivor");

        /// <summary>
        /// Mixamo FBX in <paramref name="root"/> -> model prefab with its own mesh assets, extracted textures, HDRP
        /// materials and "mixamorig:" bones. Once built, the FBX can be removed; re-running then only renames bones.
        /// </summary>
        static GameObject BuildMixamoModel(string root, string body, float height, string prefabPath, string materialPrefix)
        {
            string fbx = $"{root}/{body}.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                // Already converted and the source removed: only re-normalise bone names.
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    NormaliseBones(contents);
                    // Re-point textures on the generated materials (slot = material name after "<prefix> - <body> ").
                    string head = $"{materialPrefix} - {body} ";
                    foreach (var skin in contents.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        foreach (var material in skin.sharedMaterials.Where(m => m != null && m.name.StartsWith(head)))
                            SurvivorSkin(root, body, material.name.Substring(head.Length), materialPrefix);
                    return PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            PrepareSurvivorImport(fbx);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            try
            {
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = materialPrefix + " " + body;
                NormaliseBones(model);
                var top = model.GetComponentsInChildren<Transform>().First(bone => bone.name.EndsWith("HeadTop_End"));
                model.transform.localScale *= height / Mathf.Max(top.position.y - model.transform.position.y, 0.01f);
                if (!model.TryGetComponent(out Animation animation))
                    animation = model.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                // Drop the FBX's own T-pose take; the bot adds its rifle clips at runtime.
                AnimationUtility.SetAnimationClips(animation, new AnimationClip[0]);
                animation.clip = null;
                int index = 0;
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.updateWhenOffscreen = true;
                    // Own copy of the mesh so the prefab no longer depends on the source FBX.
                    var mesh = Object.Instantiate(skin.sharedMesh);
                    string meshPath = $"{root}/{body} mesh {index++}.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    skin.sharedMesh = mesh;
                    skin.sharedMaterials = skin.sharedMaterials.Select(material => SurvivorSkin(root, body, material.name, materialPrefix)).ToArray();
                }
                return PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        /// <summary>Copy of a Pro Rifle Pack take with every bone path on the plain "mixamorig:" prefix.</summary>
        static AnimationClip RifleClip(string file)
        {
            var source = MixamoClip("Rifle", file);
            var clip = new AnimationClip { legacy = true, frameRate = source.frameRate, wrapMode = WrapMode.Loop };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var renamed = binding;
                renamed.path = System.Text.RegularExpressions.Regex.Replace(binding.path, @"mixamorig\d+:", "mixamorig:");
                AnimationUtility.SetEditorCurve(clip, renamed, AnimationUtility.GetEditorCurve(source, binding));
            }
            string path = $"{MixamoRoot}/Animations/Rifle/{file}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>Mixamo exports vary the rig prefix (mixamorig2:, mixamorig9:...); the rifle clips and bot IK expect "mixamorig:".</summary>
        static void NormaliseBones(GameObject model)
        {
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                bone.name = System.Text.RegularExpressions.Regex.Replace(bone.name, @"^mixamorig\d+:", "mixamorig:");
        }

        static void PrepareSurvivorImport(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.animationType != ModelImporterAnimationType.Legacy;
            importer.animationType = ModelImporterAnimationType.Legacy;
            string folder = System.IO.Path.ChangeExtension(path, null) + " textures";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(folder));
                importer.ExtractTextures(folder);
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();
            // The extracted PNGs must be imported before materials look them up.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>HDRP Lit per material slot, matching the extracted map whose name shares the slot's prefix.</summary>
        static Material SurvivorSkin(string root, string body, string slot, string prefix)
        {
            string path = $"Assets/Rendering/{prefix} - {body} {slot}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            string folder = $"{root}/{body} textures";
            var maps = AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string key = slot.Split('_', ' ', '.')[0].ToLowerInvariant();
            string Pick(params string[] kinds) => maps.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant().StartsWith(key) &&
                                                      kinds.Any(k => m.ToLowerInvariant().Contains(k)))
                                                  ?? maps.FirstOrDefault(m => kinds.Any(k => m.ToLowerInvariant().Contains(k)));
            string diffuse = Pick("diffuse", "basecolor", "albedo", "color");
            string normal = Pick("normal");
            material.SetColor("_BaseColor", Color.white);
            if (diffuse != null)
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            if (normal != null)
                material.SetTexture("_NormalMap", ImportNormal(normal));
            material.SetFloat("_Smoothness", 0.3f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
