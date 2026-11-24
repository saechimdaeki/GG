using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string MaterialPassName = "Haerim water damage and evacuation debris";

        [MenuItem("잔류신호/재질과 그립 갱신")]
        public static void UpdateMaterialAndGrip()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Platform03.unity");
            var root = GameObject.Find("Platform 03").transform;
            var old = root.Find(MaterialPassName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            BuildMaterialPass(root);
            AdjustWeaponGrips(Object.FindFirstObjectByType<StationTrial>());
            var global = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{SettingsFolder}/StationVolume.asset");
            ApplyCinematicLook(global);
            EditorUtility.SetDirty(global);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        static void AdjustWeaponGrips(StationTrial trial)
        {
            var arsenal = new SerializedObject(trial).FindProperty("arsenal");
            for (int i = 0; i < arsenal.arraySize; i++)
            {
                var gun = arsenal.GetArrayElementAtIndex(i);
                var view = (Transform)gun.FindPropertyRelative("view").objectReferenceValue;
                AdjustWeaponGrip(view, i == 0);
            }
        }

        static void AdjustWeaponGrip(Transform view, bool pistol)
        {
            Transform wrist = null, elbow = null, shoulder = null;
            foreach (var bone in view.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "hand.L") wrist = bone;
                if (bone.name == "forearm.L") elbow = bone;
                if (bone.name == "upper_arm.L") shoulder = bone;
            }
            if (wrist == null || elbow == null || shoulder == null)
                return;

            Vector3 current = view.InverseTransformPoint(wrist.position);
            // Anchor the support palm to the frame or handguard in weapon space, including while aiming.
            Vector3 goal = view.TransformPoint(pistol ? new Vector3(-0.024f, -0.080f, -0.025f) :
                new Vector3(-0.018f, -0.078f, current.z));
            Quaternion wristRotation = wrist.rotation;
            float upper = Vector3.Distance(shoulder.position, elbow.position);
            float lower = Vector3.Distance(elbow.position, wrist.position);
            Vector3 towards = goal - shoulder.position;
            float reach = Mathf.Clamp(towards.magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
            Vector3 axis = towards.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(elbow.position - shoulder.position, axis).normalized;
            float longitudinal = (upper * upper + reach * reach - lower * lower) / (2f * reach);
            Vector3 desiredElbow = axis * longitudinal + bend * Mathf.Sqrt(Mathf.Max(0f, upper * upper - longitudinal * longitudinal));
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, desiredElbow) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, shoulder.position + axis * reach - elbow.position) * elbow.rotation;
            wrist.rotation = wristRotation;
        }

        static void BuildMaterialPass(Transform root)
        {
            var detail = Pivot(root, MaterialPassName, Vector3.zero);
            var porcelain = Surface("Station - patched porcelain", new Color(0.68f, 0.72f, 0.69f), 0.43f, new(1.55f, 1.25f), maps: "Wall");
            var concrete = Surface("Station - damp repair panels", new Color(0.48f, 0.52f, 0.50f), 0.31f, new(1.65f, 1.2f), maps: "Concrete");
            var wet = Surface("Station - wet floor sheen", new Color(0.22f, 0.26f, 0.25f), 0.93f);
            var stain = Surface("Station - dried blood traces", new Color(0.16f, 0.031f, 0.023f), 0.39f);
            MakePatchMaterial(wet, "Haerim wet floor alpha", 24f, 0.48f);
            MakePatchMaterial(stain, "Haerim dried stains alpha", 61f, 0.67f);
            // Textured repair bands with gaps between panels. They never change collision or the navigation mesh.
            for (int panel = 0; panel < 8; panel++)
            {
                float z = 38.9f + panel * 3.65f;
                Box(detail, "Annex wall repair panel", new(-6.358f, 0.66f, z), new(0.041f, 1.28f, 3.58f), concrete, false);
                Box(detail, "Annex wall repair panel", new(6.358f, 0.66f, z), new(0.041f, 1.28f, 3.58f), concrete, false);
            }
            var steel = Surface("Station - conduit and casework", new Color(0.26f, 0.29f, 0.28f), 0.47f);
            var enamel = Surface("Station - scuffed safety enamel", new Color(0.63f, 0.43f, 0.16f), 0.39f);
            for (int bay = 0; bay < 5; bay++)
            {
                float z = 54.5f + bay * 2.55f;
                Box(detail, "Loading bay tile repair", new(-6.36f, 1.40f, z),
                    new(0.041f, 1.65f, 2.42f), porcelain, false);
                Box(detail, "Loading bay bumper rail bracket", new(-6.25f, 0.68f, z),
                    new(0.18f, 0.26f, 0.13f), steel, false);
            }
            Box(detail, "Loading bay steel bumper rail", new(-6.17f, 0.68f, 59.65f),
                new(0.12f, 0.19f, 12.83f), steel, false);
            PipeBetween(detail, "Electrical conduit under the roof", new(-6.18f, 3.71f, 53.2f),
                new(-6.18f, 3.71f, 68.4f), 0.048f, steel);
            PipeBetween(detail, "Conduit drop beside the door", new(-3.93f, 3.68f, 68.78f),
                new(-3.93f, 1.02f, 68.78f), 0.05f, steel);
            Box(detail, "Door hinge guards and wear", new(-3.94f, 0.54f, 68.58f),
                new(0.20f, 1.05f, 0.24f), enamel, false);
            LoadProp("utility_box_01", Pivot(detail, "Loading bay weathered electrical enclosure", new(-5.82f, 0.40f, 64.9f)), 0.78f, 90f);
            Spotlight(detail, "Warm service spill across the bay", new(-4.55f, 3.18f, 66.3f),
                new(-3.4f, 0.82f, 61.95f), new Color(1f, 0.80f, 0.60f), 2750f, 78f, 10f);
            for (int panel = 0; panel < 5; panel++)
                Box(detail, "Replacement wall tile panel", new(5.695f, 1.08f, 2.9f + panel * 4.35f),
                    new(0.032f, 1.76f, 4.25f), porcelain, false);

            FloorMark(detail, "Rain tracked through the entrance", new(1.25f, 0.016f, -3.37f), new(1.45f, 0.95f), 24f, wet);
            FloorMark(detail, "Platform seepage", new(-3.48f, 0.015f, 20.7f), new(2.3f, 1.07f), 42f, wet);
            FloorMark(detail, "Annex puddle", new(-2.8f, 0.017f, 44.2f), new(2.55f, 1.22f), 118f, wet);
            FloorMark(detail, "Loading bay puddle", new(-2.2f, 0.017f, 63.0f), new(2.95f, 1.42f), 12f, wet);
            FloorMark(detail, "Old blood on platform approach", new(3.05f, 0.019f, 14.8f), new(1.5f, 0.53f), 34f, stain);
            FloorMark(detail, "Old blood by the loading bay", new(0.28f, 0.018f, 56.1f), new(1.1f, 0.55f), 122f, stain);
            FloorMark(detail, "Blood smears across the annex", new(-4.14f, 0.018f, 47.15f), new(1.1f, 0.47f), 85f, stain);

            LoadProp("trashbag", Pivot(detail, "Discarded bag under wall conduit", new(-5.68f, 0f, 42.45f)), 0.37f, 25f);
            LoadProp("plastic_crate_02", Pivot(detail, "Discarded crate at the loading bay", new(5.5f, 0f, 57.55f)), 0.35f, 78f);
            LoadProp("trashbag", Pivot(detail, "Debris by emergency door", new(4.98f, 0f, 67.3f)), 0.36f, 123f);
        }

        static void FloorMark(Transform parent, string name, Vector3 position, Vector2 size, float yaw, Material material)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.name = name;
            mark.transform.SetParent(parent, false);
            mark.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90, 0, 0));
            mark.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = mark.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void MakePatchMaterial(Material material, string name, float seed, float opacity)
        {
            string path = $"{SettingsFolder}/{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                const int size = 128;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
                    { wrapMode = TextureWrapMode.Clamp, name = name };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = 2f * x / (size - 1) - 1f;
                    float v = 2f * y / (size - 1) - 1f;
                    float roughEdge = Mathf.PerlinNoise(u * 3.6f + seed, v * 3.2f + seed) * 0.28f;
                    float rim = 0.83f + roughEdge - Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.SmoothStep(0f, 1f, rim * 8f) *
                        Mathf.Lerp(0.68f, 1f, Mathf.PerlinNoise(seed + u * 17f, seed + v * 13f)) * opacity;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, path);
            }
            material.SetTexture("_BaseColorMap", texture);
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", 0f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
        }
    }
}
