using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatChaos
{
    /// <summary>Material cache: the saved surface materials plus flat URP Lit colours made on demand.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> Cache = new();
        static Shader _lit;

        /// <summary>A material saved by Setup into Resources/Materials.</summary>
        public static Material Saved(string name)
        {
            if (Cache.TryGetValue("saved:" + name, out var m)) return m;
            m = Resources.Load<Material>("Materials/" + name) ?? Flat(Color.magenta);
            return Cache["saved:" + name] = m;
        }

        /// <summary>Plain coloured URP Lit (optionally glossy, metallic or glowing).</summary>
        public static Material Flat(Color c, float smooth = 0.3f, float metal = 0f, float glow = 0f)
        {
            string key = $"{c}|{smooth}|{metal}|{glow}";
            if (Cache.TryGetValue(key, out var m)) return m;
            if (!_lit) _lit = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(_lit);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (glow > 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            return Cache[key] = m;
        }

        /// <summary>A primitive with no collider, parented, for building small props out of shapes.</summary>
        public static GameObject Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        public static void NoShadows(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// <summary>Loose bits (shards, soil) that stay on the floor as part of the mess; oldest go first.</summary>
    public static class Debris
    {
        const int Max = 500;
        static readonly Queue<GameObject> Live = new();

        public static void Spawn(Vector3 at, Vector3 size, Material m, Vector3 velocity, float mass = 0.03f)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "debris";
            g.layer = 2;   // ignored by the camera and the cat's ground check
            g.transform.position = at;
            g.transform.rotation = Random.rotation;
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            rb.linearDamping = 0.3f;
            Live.Enqueue(g);
            while (Live.Count > Max) { var old = Live.Dequeue(); if (old) Object.Destroy(old); }
        }

        public static IEnumerable<GameObject> Pieces => Live;

        public static void Clear() => Live.Clear();
    }
}
