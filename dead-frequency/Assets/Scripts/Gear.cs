using System.Collections.Generic;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>Everything a survivor can find on the floor besides guns and supply boxes, in Left 4 Dead's slots.</summary>
    public enum Gear { PipeBomb, Molotov, Pills, Adrenaline, FireAxe, Crowbar, Bat, Machete }

    public static class GearInfo
    {
        public static bool IsThrowable(this Gear gear) => gear is Gear.PipeBomb or Gear.Molotov;
        public static bool IsMedicine(this Gear gear) => gear is Gear.Pills or Gear.Adrenaline;
        public static bool IsMelee(this Gear gear) => gear >= Gear.FireAxe;

        public static string Label(this Gear gear) => gear switch
        {
            Gear.PipeBomb => "파이프 폭탄",
            Gear.Molotov => "화염병",
            Gear.Pills => "진통제",
            Gear.Adrenaline => "아드레날린",
            Gear.FireAxe => "소방 도끼",
            Gear.Crowbar => "쇠지렛대",
            Gear.Bat => "야구 방망이",
            _ => "마체테",
        };

        /// <summary>The label with its object particle: 진통제를, 화염병을.</summary>
        public static string LabelObject(this Gear gear)
        {
            string label = gear.Label();
            char last = label[^1];
            bool batchim = last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0;
            return label + (batchim ? "을" : "를");
        }

        public static Throwable.Kind AsThrowable(this Gear gear) => gear == Gear.PipeBomb ? Throwable.Kind.PipeBomb : Throwable.Kind.Molotov;

        /// <summary>The object as it lies on the floor, pivot on the floor-contact point.</summary>
        public static GameObject Model(Gear gear, Vector3 point, float yaw)
        {
            if (gear.IsThrowable())
            {
                // Lying on its side, the pipe bomb's timer facing up.
                float radius = gear == Gear.PipeBomb ? 0.034f : 0.04f;
                return Throwable.Model(gear.AsThrowable(), point + Vector3.up * radius, Quaternion.Euler(-90f, yaw, 0f));
            }
            if (gear.IsMelee())
            {
                var weapon = MeleeWeapon.Model(gear);
                // Laid flat: the weapon's length along the floor.
                weapon.transform.SetPositionAndRotation(point + Vector3.up * 0.025f, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 90f));
                return weapon;
            }
            return Medicine(gear, point, yaw);
        }

        /// <summary>A brown pill bottle with a white cap, or a yellow auto-injector.</summary>
        static GameObject Medicine(Gear gear, Vector3 point, float yaw)
        {
            var root = new GameObject(gear.Label());
            root.layer = 2;
            root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            var collider = root.AddComponent<BoxCollider>();
            if (gear == Gear.Pills)
            {
                collider.size = new Vector3(0.05f, 0.1f, 0.05f);
                collider.center = Vector3.up * 0.05f;
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.048f, 0.04f, 0.048f), Lit("Pill bottle", new Color(0.55f, 0.27f, 0.06f, 1f), 0f, 0.8f));
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.088f, 0f), new Vector3(0.052f, 0.01f, 0.052f), Lit("Pill cap", new Color(0.92f, 0.92f, 0.9f), 0f, 0.5f));
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.0495f, 0.018f, 0.0495f), Lit("Pill label", new Color(0.95f, 0.95f, 0.92f), 0f, 0.2f));
            }
            else
            {
                // Lying down: an auto-injector pen.
                collider.size = new Vector3(0.03f, 0.03f, 0.17f);
                collider.center = Vector3.up * 0.015f;
                var pen = Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.028f, 0.07f, 0.028f), Lit("Injector", new Color(0.95f, 0.72f, 0.1f), 0.1f, 0.6f));
                pen.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var tip = Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0.077f), new Vector3(0.022f, 0.008f, 0.022f), Lit("Injector tip", new Color(0.85f, 0.15f, 0.1f), 0f, 0.6f));
                tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var cap = Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, -0.077f), new Vector3(0.03f, 0.01f, 0.03f), Lit("Injector cap", new Color(0.15f, 0.35f, 0.75f), 0f, 0.6f));
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return root;
        }

        static GameObject Part(GameObject root, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            Object.Destroy(part.GetComponent<Collider>());
            part.layer = 2;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        static readonly Dictionary<string, Material> materials = new();

        internal static Material Lit(string name, Color color, float metallic, float smoothness, Texture texture = null)
        {
            if (materials.TryGetValue(name, out var cached))
                return cached;
            var material = new Material(Shader.Find("HDRP/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (texture != null)
                material.SetTexture("_BaseColorMap", texture);
            materials[name] = material;
            return material;
        }
    }

    /// <summary>A piece of gear lying in the station. E takes it into its slot, leaving what you carried in its place.</summary>
    public sealed class GearPickup : MonoBehaviour
    {
        public static readonly List<GearPickup> All = new();
        public Gear gear;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
