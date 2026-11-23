using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    /// <summary>Primary guns (shotgun, rifle, bolt-action) with their own posed arms, plus the station gun rack.</summary>
    public static partial class StationSceneBuilder
    {
        struct GunSpec
        {
            public string label, model, folder, baseColor, normal, mask, sound;
            public float length, pitch, fireInterval, reload, spread, kick;
            public int capacity, reserve, damage, pellets;
            public bool automatic, pierces;
        }

        static readonly GunSpec[] Primaries =
        {
            new() { label = "산탄총", sound = "Gun_Shotgun", model = "shotgun.obj", folder = "Shotgun", baseColor = "BaseColor.jpg", mask = "Mask.png",
                length = 0.58f, pitch = 0.68f, fireInterval = 0.42f, reload = 2.1f, spread = 5.5f, kick = 2.1f,
                capacity = 2, reserve = 24, damage = 22, pellets = 9 },
            new() { label = "AK 소총", sound = "Gun_Rifle", model = "AK.fbx", folder = "Rifle", baseColor = "AK_1_Albedo.png", normal = "AK_1_Normal.png",
                mask = "Mask.png", length = 0.88f, pitch = 0.95f, fireInterval = 0.105f, reload = 1.9f, spread = 0f, kick = 0.7f,
                capacity = 30, reserve = 150, damage = 34, pellets = 1, automatic = true },
            new() { label = "저격소총", sound = "Gun_Bolt", model = "rifle.fbx", folder = "BoltRifle", baseColor = "BaseColor.jpg", normal = "Normal.jpg",
                mask = "Mask.png", length = 1.08f, pitch = 0.58f, fireInterval = 1.05f, reload = 2.6f, spread = 0f, kick = 2.4f,
                capacity = 5, reserve = 30, damage = 120, pellets = 1, pierces = true },
            new() { label = "반자동 카빈", sound = "Carbine", model = "m16.obj", folder = "Carbine",
                length = 0.78f, pitch = 1f, fireInterval = 0.20f, reload = 1.65f, spread = 0f, kick = 0.52f,
                capacity = 20, reserve = 100, damage = 47, pellets = 1 },
        };

        static void CreateArsenal(SerializedObject game, Camera camera, Material flash, Transform pistol,
            Transform magazine, Transform slide, Transform supportHand, GameObject pistolMuzzle)
        {
            var shot = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Gun_Pistol.wav");
            var arsenal = game.FindProperty("arsenal");
            arsenal.arraySize = Primaries.Length + 1;
            var sidearm = arsenal.GetArrayElementAtIndex(0);
            WriteGun(sidearm, "권총", pistol, pistolMuzzle, shot, 1f, 9, 72, 0.15f, 1.2f, 50, 1, 0, 1f, false, false);
            sidearm.FindPropertyRelative("magazine").objectReferenceValue = magazine;
            sidearm.FindPropertyRelative("slide").objectReferenceValue = slide;
            sidearm.FindPropertyRelative("supportHand").objectReferenceValue = supportHand;

            var rack = Pivot(null, "Station gun rack", new(3.35f, 0, 2.2f));
            var steel = Surface("Steel - powder coat", new Color(0.30f, 0.36f, 0.37f), 0.59f);
            Box(rack, "Rack table", new(0, 0.42f, 0), new(1.0f, 0.84f, 2.3f), steel);
            var pickups = game.FindProperty("pickups");
            pickups.arraySize = Primaries.Length;
            for (int index = 0; index < Primaries.Length; index++)
            {
                var spec = Primaries[index];
                var material = GunMaterial(spec);
                var view = Pivot(camera.transform, spec.label + " viewmodel", new(0.19f, -0.175f, 0.28f));
                view.localRotation = Quaternion.Euler(1.5f, -5f, 0f);
                var model = InstallLongGun(view, spec, material, out Vector3 muzzlePoint, out float top, out float rear);
                float length = spec.length;
                var left = InstallArms(view, new(0.17f, -0.22f, -0.36f), new(0.0f, top - 0.085f, rear + length * 0.27f),
                    new(-0.24f, -0.34f, -0.24f), new(-0.025f, top - 0.105f, rear + length * 0.58f), longGun: true);
                var element = arsenal.GetArrayElementAtIndex(index + 1);
                WriteGun(element, spec.label, view, MuzzleBurst(view, muzzlePoint, flash),
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Art/Audio/{spec.sound}.wav"), 1f, spec.capacity,
                    spec.reserve, spec.fireInterval, spec.reload, spec.damage, spec.pellets, spec.spread, spec.kick,
                    spec.automatic, spec.pierces);
                element.FindPropertyRelative("supportHand").objectReferenceValue = left;
                element.FindPropertyRelative("magazine").objectReferenceValue = null;
                element.FindPropertyRelative("slide").objectReferenceValue = null;

                bool carbine = spec.folder == "Carbine";
                float rackZ = carbine ? 2.60f : -0.72f + index * 0.72f;
                if (carbine)
                    Box(rack, "Carbine table", new(0, 0.42f, rackZ), new(1f, 0.84f, 1.05f), steel);
                var slot = Pivot(rack, spec.label + " pickup", new(0, 0.86f, rackZ));
                var display = Object.Instantiate(model, slot);
                display.transform.localPosition = Vector3.zero;
                display.transform.localRotation = carbine ? Quaternion.Euler(0, 90f, 90f) : Quaternion.Euler(90f, 0, 0);
                SetLayerRecursive(display, 0);
                slot.gameObject.AddComponent<WeaponPickup>().arsenalIndex = index + 1;
                pickups.GetArrayElementAtIndex(index).objectReferenceValue = slot.GetComponent<WeaponPickup>();
            }
            StationLight(rack, new(0, 2.2f, 0), new Color(1f, 0.82f, 0.6f), 900, 4.5f);
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = layer;
        }

        static void WriteGun(SerializedProperty gun, string label, Transform view, GameObject muzzle, AudioClip shot,
            float pitch, int capacity, int reserve, float interval, float reload, int damage, int pellets, float spread,
            float kick, bool automatic, bool pierces)
        {
            gun.FindPropertyRelative("label").stringValue = label;
            gun.FindPropertyRelative("view").objectReferenceValue = view;
            gun.FindPropertyRelative("muzzle").objectReferenceValue = muzzle;
            gun.FindPropertyRelative("shot").objectReferenceValue = shot;
            gun.FindPropertyRelative("pitch").floatValue = pitch;
            gun.FindPropertyRelative("capacity").intValue = capacity;
            gun.FindPropertyRelative("startingReserve").intValue = reserve;
            gun.FindPropertyRelative("fireInterval").floatValue = interval;
            gun.FindPropertyRelative("reloadDuration").floatValue = reload;
            gun.FindPropertyRelative("damage").intValue = damage;
            gun.FindPropertyRelative("pellets").intValue = pellets;
            gun.FindPropertyRelative("spread").floatValue = spread;
            gun.FindPropertyRelative("kick").floatValue = kick;
            gun.FindPropertyRelative("automatic").boolValue = automatic;
            gun.FindPropertyRelative("pierces").boolValue = pierces;
        }

        static GameObject MuzzleBurst(Transform weapon, Vector3 point, Material flash)
        {
            var muzzle = Pivot(weapon, "Muzzle burst", point);
            var flame = Shape(muzzle, PrimitiveType.Sphere, "Flame core", new(0, 0, 0.038f), new(0.055f, 0.028f, 0.14f), flash);
            flame.localRotation = Quaternion.Euler(0, 0, 18f);
            var lamp = muzzle.gameObject.AddComponent<Light>();
            lamp.color = new Color(1, 0.53f, 0.19f);
            lamp.range = 5f;
            lamp.intensity = 165f;
            muzzle.gameObject.SetActive(false);
            return muzzle.gameObject;
        }

        static Material GunMaterial(GunSpec spec)
        {
            string folder = $"Assets/Art/Models/{spec.folder}/";
            if (spec.baseColor == null)
                return Surface("Weapon - carbine receiver", new Color(0.20f, 0.22f, 0.21f), 0.55f);
            var material = Surface("Weapon - " + spec.folder.ToLowerInvariant(), Color.white, 0.6f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + spec.baseColor));
            if (spec.normal != null)
                material.SetTexture("_NormalMap", ImportNormal(folder + spec.normal));
            var mask = (TextureImporter)AssetImporter.GetAtPath(folder + spec.mask);
            if (mask.sRGBTexture)
            {
                mask.sRGBTexture = false;
                mask.SaveAndReimport();
            }
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + spec.mask));
            material.SetFloat("_MetallicRemapMax", 1f);
            material.SetFloat("_SmoothnessRemapMax", 0.85f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Lays the gun along the view's +Z with the stock (taller end) toward the camera.</summary>
        static GameObject InstallLongGun(Transform view, GunSpec spec, Material material, out Vector3 muzzlePoint,
            out float top, out float rear)
        {
            string folder = $"Assets/Art/Models/{spec.folder}/";
            bool carbine = spec.folder == "Carbine";
            GameObject model;
            if (carbine)
            {
                model = Pivot(view, "Carbine model", Vector3.zero).gameObject;
                ImportModel(folder + spec.model, model.transform, material);
                var magazine = Surface("Weapon - carbine magazine", new Color(0.24f, 0.26f, 0.25f), 0.63f);
                ImportModel(folder + "magazine.obj", model.transform, magazine);
            }
            else
            {
                model = ImportModel(folder + spec.model, view, material);
            }
            if (spec.folder == "BoltRifle")
                AssignAccessories(model);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, carbine ? 180f : 90f, 0);
            Vector3[] points = LocalVertices(model, view);
            Bounds bounds = Enclose(points);
            model.transform.localScale *= spec.length / Mathf.Max(0.01f, bounds.size.z);
            points = LocalVertices(model, view);
            bounds = Enclose(points);
            rear = -0.05f;
            model.transform.localPosition += new Vector3(-bounds.center.x, -bounds.max.y, rear - bounds.min.z);
            top = 0f;
            muzzlePoint = new Vector3(0, -0.03f, rear + spec.length + 0.02f);
            return model;
        }

        static void AssignAccessories(GameObject model)
        {
            const string folder = "Assets/Art/Models/BoltRifle/";
            var extra = Surface("Weapon - bolt rifle accessories", Color.white, 0.6f);
            extra.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "AccessoriesColor.png"));
            extra.SetTexture("_NormalMap", ImportNormal(folder + "AccessoriesNormal.jpg"));
            var mask = (TextureImporter)AssetImporter.GetAtPath(folder + "AccessoriesMask.png");
            if (mask.sRGBTexture)
            {
                mask.sRGBTexture = false;
                mask.SaveAndReimport();
            }
            extra.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "AccessoriesMask.png"));
            extra.SetFloat("_AlphaCutoffEnable", 1f);
            extra.SetFloat("_AlphaCutoff", 0.5f);
            HDMaterial.ValidateMaterial(extra);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "rifle.fbx");
            var originals = source.GetComponentsInChildren<Renderer>(true);
            var copies = model.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < copies.Length && index < originals.Length; index++)
            {
                var slots = copies[index].sharedMaterials;
                var names = originals[index].sharedMaterials;
                for (int slot = 0; slot < slots.Length && slot < names.Length; slot++)
                    if (names[slot] != null && names[slot].name.Contains("acces"))
                        slots[slot] = extra;
                copies[index].sharedMaterials = slots;
            }
        }

        static Vector3[] LocalVertices(GameObject model, Transform space) =>
            model.GetComponentsInChildren<Renderer>(true)
                .Select(renderer => (renderer.transform, mesh: renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh))
                .Where(part => part.mesh != null)
                .SelectMany(part => part.mesh.vertices.Select(vertex =>
                    space.InverseTransformPoint(part.transform.TransformPoint(vertex))))
                .ToArray();

        static Bounds Enclose(Vector3[] points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
                bounds.Encapsulate(point);
            return bounds;
        }

        static float Height(System.Collections.Generic.IEnumerable<Vector3> points)
        {
            float low = float.MaxValue, high = float.MinValue;
            foreach (var point in points)
            {
                low = Mathf.Min(low, point.y);
                high = Mathf.Max(high, point.y);
            }
            return high - low;
        }
    }
}
