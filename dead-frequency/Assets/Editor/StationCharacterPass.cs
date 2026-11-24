using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string CuffName = "Textile wrist closure";

        [MenuItem("잔류신호/인물 모델 재질과 장비 갱신")]
        public static void UpdateCharacterArt()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var trial = Object.FindFirstObjectByType<StationTrial>();
            var arsenal = new SerializedObject(trial).FindProperty("arsenal");
            for (int i = 0; i < arsenal.arraySize; i++)
                FinishRescueArms((Transform)arsenal.GetArrayElementAtIndex(i).FindPropertyRelative("view").objectReferenceValue);

            ApplyCharacterMaterials();
            foreach (string prefab in new[] { "Rescuer", "RescuerBot" })
            {
                string path = $"Assets/Prefabs/{prefab}.prefab";
                var actor = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AddRescuerKit(actor.transform);
                    PrefabUtility.SaveAsPrefabAsset(actor, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(actor);
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        static void ApplyCharacterMaterials()
        {
            Texture2D fabric = MaterialTexture("RescueClothDetail.jpg", normal: true);
            foreach (string label in new[] { "turnout coat", "navy coat", "rust coat" })
                SetFineNormal($"Rescuer - {label}", fabric, 6f, 0.56f, 0.23f);

            foreach (string material in AssetDatabase.FindAssets("t:Material", new[] { SettingsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(material);
                string label = System.IO.Path.GetFileNameWithoutExtension(path);
                if (label.StartsWith("Infected civilian - ") && label.EndsWith(" body"))
                    SetFineNormal(label, fabric, 7f, 0.41f, 0.19f);
            }

            var giant = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Loading mutant - matte skin.mat");
            giant.SetColor("_BaseColor", new Color(0.77f, 0.75f, 0.69f));
            SetFineNormal(giant.name, MaterialTexture("RescueLeatherDetail.jpg", normal: true), 8f, 0.38f, 0.18f);
        }

        static void AddRescuerKit(Transform actor)
        {
            var chest = System.Array.Find(actor.GetComponentsInChildren<Transform>(true),
                node => node.name == "Bip01 Spine2");
            if (chest == null)
                return;
            var existing = chest.Find("Haerim rescue radio");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
            var shell = Surface("Equipment - field radio shell", new Color(0.085f, 0.102f, 0.096f), 0.36f);
            var trim = Surface("Equipment - field radio trim", new Color(0.28f, 0.26f, 0.21f), 0.42f);
            var radio = Pivot(chest, "Haerim rescue radio", Vector3.zero);
            radio.SetPositionAndRotation(chest.position + actor.forward * 0.19f - actor.right * 0.16f + actor.up * 0.045f,
                actor.rotation * Quaternion.Euler(0f, -9f, 12f));
            radio.localScale /= Mathf.Max(0.001f, Mathf.Abs(radio.lossyScale.x));
            Box(radio, "Radio body", Vector3.zero, new Vector3(0.069f, 0.11f, 0.037f), shell, false);
            Box(radio, "Radio display", new Vector3(0.006f, 0.021f, 0.019f), new Vector3(0.047f, 0.023f, 0.004f), trim, false);
            Shape(radio, PrimitiveType.Cylinder, "Rubber antenna", new Vector3(-0.024f, 0.101f, 0f),
                new Vector3(0.009f, 0.06f, 0.009f), shell);
        }

        static void SetFineNormal(string label, Texture2D detail, float repeats, float strength, float sheen)
        {
            var surface = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/{label}.mat");
            if (surface == null)
                return;
            if (surface.GetTexture("_NormalMap") == null || surface.GetTexture("_NormalMap") == detail)
            {
                surface.SetTexture("_NormalMap", detail);
                surface.SetFloat("_NormalScale", strength);
            }
            else
            {
                // HDRP detail map packs normal Y/X in G/A. The cloth normal remains separate from the baked form.
                var packed = PackedCharacterDetail(detail);
                surface.SetTexture("_DetailMap", packed);
                surface.SetTextureScale("_DetailMap", Vector2.one * repeats);
                surface.SetFloat("_DetailNormalScale", strength);
                surface.SetFloat("_DetailAlbedoScale", 0f);
                surface.SetFloat("_DetailSmoothnessScale", 0f);
            }
            surface.SetFloat("_Smoothness", sheen);
            HDMaterial.ValidateMaterial(surface);
            EditorUtility.SetDirty(surface);
        }

        static Texture2D PackedCharacterDetail(Texture2D normal)
        {
            string path = $"{SettingsFolder}/{normal.name} packed.asset";
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (packed != null)
                return packed;

            var rt = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prior = RenderTexture.active;
            packed = new Texture2D(512, 512, TextureFormat.RGBA32, true, true) { name = normal.name + " packed" };
            try
            {
                // Read the source JPEG as RGB. Blitting an imported normal map would read the platform's packed channels.
                var raw = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
                raw.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(normal)));
                Graphics.Blit(raw, rt);
                Object.DestroyImmediate(raw);
                RenderTexture.active = rt;
                packed.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                var pixels = packed.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(128, pixels[i].g, 128, pixels[i].r);
                packed.SetPixels32(pixels);
                packed.Apply();
            }
            finally
            {
                RenderTexture.active = prior;
                RenderTexture.ReleaseTemporary(rt);
            }
            AssetDatabase.CreateAsset(packed, path);
            return packed;
        }

        static void FinishRescueArms(Transform view)
        {
            var leather = MaterialTexture("RescueLeatherDetail.jpg", normal: true);
            SetFineNormal("Equipment - rigged rescue arms", leather, 3.5f, 0.48f, 0.34f);
            var cuff = Surface("Equipment - cuff webbing", new Color(0.095f, 0.11f, 0.105f), 0.30f);
            var edge = Surface("Equipment - cuff binding", new Color(0.22f, 0.245f, 0.21f), 0.29f);
            var bones = view.GetComponentsInChildren<Transform>(true);
            foreach (var wrist in bones)
            {
                if (wrist == null || (wrist.name != "hand.L" && wrist.name != "hand.R"))
                    continue;
                var old = wrist.Find(CuffName) ?? wrist.Find("Reinforced rescue cuff");
                bool firstPass = old == null || old.name != CuffName;
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                if (firstPass && wrist.name == "hand.L" && view.name.Contains("pistol"))
                {
                    Vector3 palm = view.TransformDirection(new Vector3(0.83f, 0.55f, 0f));
                    foreach (string digit in new[] { "index", "middle", "ring", "pinky" })
                    for (int joint = 1; joint <= 2; joint++)
                    {
                        Transform finger = System.Array.Find(bones, node => node != null && node.name == $"f_{digit}.0{joint}.L");
                        Transform next = System.Array.Find(bones, node => node != null && node.name == $"f_{digit}.0{joint + 1}.L");
                        if (finger == null || next == null)
                            continue;
                        Vector3 curl = Vector3.Cross(next.position - finger.position, palm);
                        finger.rotation = Quaternion.AngleAxis(joint == 1 ? 14f : 9f, curl) * finger.rotation;
                    }
                }
                Transform forearm = System.Array.Find(bones, node => node != null && node.name == "forearm." + wrist.name[^1]);
                if (forearm == null)
                    continue;
                Vector3 axis = (wrist.position - forearm.position).normalized;
                var group = Pivot(wrist, CuffName, Vector3.zero);
                group.SetPositionAndRotation(wrist.position - axis * 0.019f, Quaternion.FromToRotation(Vector3.up, axis));
                float scale = Mathf.Max(0.001f, Mathf.Abs(group.lossyScale.x));
                group.localScale /= scale;
                Shape(group, PrimitiveType.Cylinder, "Woven wrist closure", Vector3.zero,
                    new Vector3(0.053f, 0.012f, 0.049f), cuff);
                Shape(group, PrimitiveType.Cylinder, "Stitched cuff lip", Vector3.up * 0.010f,
                    new Vector3(0.054f, 0.0015f, 0.050f), edge);
            }
        }
    }
}
