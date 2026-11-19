using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot project setup and build, runnable in batch mode:
///   Unity -batchmode -quit -projectPath . -executeMethod Setup.Run
///   Unity -batchmode -quit -projectPath . -executeMethod Setup.BuildMac
/// Creates the shared materials (so their shaders ship in the build), the Main scene with
/// the Game object, and registers the scene.
/// </summary>
public static class Setup
{
    const string Mats = "Assets/Resources/Materials";

    [MenuItem("Last Train/Setup")]
    public static void Run()
    {
        Directory.CreateDirectory(Mats);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        HavenImport.BuildMaterials();
        Save(Toon("asphalt_02", 6f, new Color(0.40f, 0.42f, 0.48f), 0.6f, 0.3f), "Ground");
        Save(Toon("concrete_pavement", 2.2f, new Color(0.80f, 0.77f, 0.70f), 0.4f, 0.15f), "Sidewalk");
        Save(Toon("red_brick_03", 2.6f, new Color(0.68f, 0.42f, 0.36f), 0.3f, 0f), "Brick");
        Save(Toon("concrete_tile_facade", 2.4f, new Color(0.88f, 0.85f, 0.76f), 0.4f, 0f), "FacadeTile");
        Save(Toon("plastered_wall_04", 3f, new Color(0.94f, 0.90f, 0.82f), 0.3f, 0f), "Plaster");
        Save(Toon("painted_metal_shutter", 1.6f, new Color(0.56f, 0.66f, 0.72f), 0.5f, 0f), "Shutter");
        Save(Toon("rusty_metal_sheet", 2f, new Color(0.70f, 0.46f, 0.32f), 0.4f, 0f), "Rust");
        Save(Toon("floor_tiles_06", 2f, new Color(0.90f, 0.88f, 0.82f), 0.6f, 0f), "FloorTile");
        Save(Toon("old_linoleum_flooring_01", 2.5f, new Color(0.72f, 0.80f, 0.68f), 0.5f, 0f), "Linoleum");
        Save(Toon("concrete_floor_worn_001", 3f, new Color(0.66f, 0.64f, 0.60f), 0.4f, 0f), "WornFloor");
        Save(Toon("grey_roof_tiles_02", 2f, new Color(0.62f, 0.44f, 0.40f), 0.4f, 0f), "RoofTile");
        Save(Toon("plank_flooring_02", 2.4f, new Color(0.52f, 0.40f, 0.31f), 0.35f, 0f), "Planks");
        Save(Toon("painted_metal_shutter", 1.2f, new Color(0.30f, 0.38f, 0.46f), 0.85f, 0f), "Glass");
        Save(Toon("painted_concrete", 2.5f, new Color(0.84f, 0.82f, 0.76f), 0.3f, 0f), "PaintedConcrete");
        var plain = new Material(lit);
        plain.SetColor("_BaseColor", new Color(0.55f, 0.54f, 0.52f));
        plain.SetFloat("_Smoothness", 0.2f);
        plain.enableInstancing = true;
        Save(plain, "Plain");

        var puddle = new Material(lit);
        puddle.SetColor("_BaseColor", new Color(0.04f, 0.045f, 0.055f));
        puddle.SetFloat("_Smoothness", 0.97f);
        puddle.SetFloat("_Metallic", 0.4f);
        Save(puddle, "Puddle");

        var concrete = Tri("dirty_concrete", 2.5f, new Color(0.55f, 0.54f, 0.52f), 0.5f, 0f);
        concrete.SetFloat("_Gain", 2f);
        concrete.SetFloat("_TexAmount", 0.5f);
        Save(concrete, "Concrete");

        Save(Glow(lit, new Color(0.25f, 1f, 0.45f), 2.5f), "Gem");
        Save(Glow(lit, new Color(1f, 0.85f, 0.5f), 2f), "Bulb");
        Save(Glow(lit, new Color(0.3f, 1f, 0.55f), 3f), "SignGreen");
        Save(Glow(lit, Color.white, 2f), "Neon");

        Save(Transparent(particles, additive: true), "FxAdd");
        Save(Transparent(particles, additive: false), "FxAlpha");
        LegacyCharacters();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Game").AddComponent<LastTrain.Game>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        PlayerSettings.productName = "막차까지";
        PlayerSettings.companyName = "GG";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.usePlayerLog = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[setup] done");
    }

    /// <summary>The cute characters must import with their legacy clips; fix any importer left in another mode.</summary>
    static void LegacyCharacters()
    {
        foreach (var file in Directory.GetFiles("Assets/Resources/Art/Characters", "*.fbx"))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(file);
            if (mi.animationType == ModelImporterAnimationType.Legacy && mi.importAnimation) continue;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.SaveAndReimport();
            Debug.Log("[setup] legacy clips restored for " + file);
        }
    }

    /// <summary>
    /// The Mixamo clips copied their avatar from a deleted "survivor" model, so its reference pose
    /// pitched the pelvis backward. Each clip builds an avatar from its own mixamorig skeleton instead.
    /// </summary>
    public static void LocomotionAvatars()
    {
        foreach (var file in Directory.GetFiles("Assets/Resources/Art/Locomotion", "*.fbx"))
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(file);
            var clips = mi.clipAnimations;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.sourceAvatar = null;
            mi.humanDescription = new HumanDescription();
            mi.autoGenerateAvatarMappingIfUnspecified = true;
            if (clips.Length > 0) mi.clipAnimations = clips;
            mi.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(file).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[setup] {file} own avatar valid={avatar && avatar.isValid} human={avatar && avatar.isHuman}");
        }
    }

    public static void BuildMac()
    {
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/Mac/LastTrain.app",
            target = BuildTarget.StandaloneOSX,
        });
        Debug.Log("[build] " + r.summary.result + " " + r.summary.totalSize / 1024 / 1024 + "MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    /// <summary>World-tiled PBR surface from a Poly Haven texture set (diffuse, normal, AO/rough/metal).</summary>
    static Material Tri(string id, float tile, Color tint, float smooth, float wet)
    {
        const string dir = "Assets/Resources/Art/Haven/Surfaces/";
        var m = new Material(Shader.Find("LastTrain/Triplanar"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + id + "_d.jpg"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + id + "_n.jpg"));
        m.SetTexture("_ArmMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + id + "_arm.jpg"));
        m.SetFloat("_Tile", tile);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Wet", wet);
        m.enableInstancing = true;
        return m;
    }

    /// <summary>
    /// Cartoon surface: a flat, bright base color with the photo texture kept only as soft detail
    /// (texture amount 0.35, gain 2 so an average texel lands on the base color).
    /// </summary>
    static Material Toon(string id, float tile, Color color, float smooth, float wet)
    {
        var m = Tri(id, tile, color, smooth, wet);
        m.SetFloat("_Gain", 2f);
        m.SetFloat("_TexAmount", 0.35f);
        return m;
    }

    static Material Glow(Shader lit, Color c, float intensity)
    {
        var m = new Material(lit);
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.enableInstancing = true;
        return m;
    }

    static Material Transparent(Shader s, bool additive)
    {
        var m = new Material(s);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", Color.white);
        m.enableInstancing = true;
        return m;
    }

    static void Save(Material m, string name)
    {
        var path = $"{Mats}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }
}
