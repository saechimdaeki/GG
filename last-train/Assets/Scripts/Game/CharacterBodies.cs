using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LastTrain
{
    /// <summary>Adult-proportioned clothed bodies skinned onto the existing animation skeletons.</summary>
    internal sealed class CharacterBodies
    {
        readonly Transform _root;
        readonly Transform[] _bones;
        readonly List<Vector3> _vertices = new();
        readonly List<BoneWeight> _weights = new();
        readonly List<int>[] _indices = { new(), new(), new(), new() };

        CharacterBodies(Transform root, Transform[] bones) { _root = root; _bones = bones; }

        public static void Replace(GameObject model, bool player, int outfit, Color cloth, Color trousers)
        {
            var original = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var names = model.GetComponentsInChildren<Transform>().ToDictionarySafe();
            string[] joints = { "Hips", "Torso", "Head", "UpperArm.L", "LowerArm.L", "Fist.L", "UpperLeg.L",
                "LowerLeg.L", "Foot.L", "UpperArm.R", "LowerArm.R", "Fist.R", "UpperLeg.R", "LowerLeg.R", "Foot.R" };
            if (joints.Any(name => !names.ContainsKey(name))) return;

            // Build in actor space with the face, toes and jacket closure pointing along +Z.
            var body = new CharacterBodies(model.transform.parent, joints.Select(name => names[name]).ToArray());
            var bones = body._bones;
            Vector3 shoulders = (bones[3].position + bones[9].position) * 0.5f;
            Vector3 hips = bones[0].position;
            Vector3 neck = new Vector3(shoulders.x, shoulders.y + 0.045f, shoulders.z);
            body.Shell(1, hips - Vector3.up * 0.035f, neck, 0.25f, 0.155f, 0.19f, 0.125f, 0);
            body.Shell(0, hips - Vector3.up * 0.13f, hips + Vector3.up * 0.055f, 0.158f, 0.115f, 0.169f, 0.12f, 1);
            Vector3 face = bones[2].position + new Vector3(0, 0.055f, 0.028f);
            body.Shell(2, face - Vector3.up * 0.035f, face + Vector3.up * 0.17f, 0.088f, 0.08f, 0.070f, 0.075f, 2);
            body.Shell(2, face + new Vector3(0, 0.08f, -0.021f), face + new Vector3(0, 0.215f, -0.002f),
                0.088f, 0.091f, 0.077f, 0.075f, 3);
            body.Shell(1, hips + new Vector3(0, 0.04f, 0.133f), neck + new Vector3(0, -0.06f, 0.151f),
                0.015f, 0.016f, 0.012f, 0.013f, 3);

            foreach (int arm in new[] { 3, 9 })
            {
                body.Limb(arm, arm + 1, 0.077f, 0.064f, 0);
                body.Limb(arm + 1, arm + 2, 0.061f, 0.038f, outfit >= 0 ? 2 : 0);
                Vector3 hand = bones[arm + 2].position;
                body.Shell(arm + 2, hand - Vector3.up * 0.035f, hand + Vector3.up * 0.045f, 0.047f, 0.039f, 0.037f, 0.028f,
                    player ? 3 : 2);
                int leg = arm + 3;
                body.Limb(leg, leg + 1, 0.091f, 0.068f, 1);
                body.Limb(leg + 1, leg + 2, 0.065f, 0.052f, 1);
                Vector3 foot = bones[leg + 2].position;
                body.Shell(leg + 2, foot + new Vector3(0, -0.01f, -0.052f), foot + new Vector3(0, -0.015f, 0.14f),
                    0.062f, 0.059f, 0.04f, 0.07f, 3);
            }

            if (player)
            {
                Vector3 belt = hips + new Vector3(0, 0.10f, -0.148f);
                body.Shell(1, belt, belt + Vector3.up * 0.38f, 0.163f, 0.105f, 0.149f, 0.100f, 3);
                body.Shell(1, belt + new Vector3(0.078f, 0.10f, -0.09f), belt + new Vector3(0.078f, 0.29f, -0.09f),
                    0.052f, 0.035f, 0.041f, 0.033f, 0);
            }

            Color skin = outfit >= 0 ? new Color(0.42f, 0.43f, 0.36f) : new Color(0.70f, 0.54f, 0.42f);
            body.Attach(new[] { cloth, trousers, skin, new Color(0.18f, 0.19f, 0.17f) });
            foreach (var renderer in original) renderer.enabled = false;
        }

        void Limb(int first, int next, float wide, float narrow, int material)
        {
            Vector3 start = _bones[first].position, end = _bones[next].position;
            Shell(first, Vector3.LerpUnclamped(start, end, -0.09f), Vector3.LerpUnclamped(start, end, 1.08f),
                wide, wide * 0.88f, narrow, narrow * 0.85f, material);
        }

        void Shell(int bone, Vector3 from, Vector3 to, float bottomX, float bottomZ, float topX, float topZ, int material)
        {
            Vector3 axis = (to - from).normalized;
            Vector3 u = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            if (u.sqrMagnitude < 0.1f) u = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            int start = _vertices.Count;
            const int sides = 8;
            for (int ring = 0; ring < 4; ring++)
            {
                float height = ring switch { 0 => 0f, 1 => 0.17f, 2 => 0.78f, _ => 1f };
                float bevel = ring == 0 || ring == 3 ? 0.65f : 1f;
                Vector3 center = Vector3.Lerp(from, to, height);
                for (int side = 0; side < sides; side++)
                {
                    float angle = (side + 0.5f) * Mathf.PI * 2f / sides;
                    Vector3 p = center + u * (Mathf.Cos(angle) * Mathf.Lerp(bottomX, topX, height) * bevel)
                        + v * (Mathf.Sin(angle) * Mathf.Lerp(bottomZ, topZ, height) * bevel);
                    _vertices.Add(_root.InverseTransformPoint(p));
                    _weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                }
            }
            var indices = _indices[material];
            for (int ring = 0; ring < 3; ring++)
            for (int side = 0; side < sides; side++)
            {
                int a = start + ring * sides + side;
                int b = start + ring * sides + (side + 1) % sides;
                indices.Add(a); indices.Add(b); indices.Add(b + sides);
                indices.Add(a); indices.Add(b + sides); indices.Add(a + sides);
            }
            for (int side = 1; side < sides - 1; side++)
            {
                indices.Add(start); indices.Add(start + side + 1); indices.Add(start + side);
                int top = start + 3 * sides;
                indices.Add(top); indices.Add(top + side); indices.Add(top + side + 1);
            }
        }

        void Attach(Color[] colors)
        {
            var mesh = new Mesh { name = "Last Train clothing and body" };
            mesh.SetVertices(_vertices);
            mesh.boneWeights = _weights.ToArray();
            mesh.bindposes = _bones.Select(bone => bone.worldToLocalMatrix * _root.localToWorldMatrix).ToArray();
            mesh.subMeshCount = _indices.Length;
            for (int i = 0; i < _indices.Length; i++) mesh.SetTriangles(_indices[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var node = new GameObject("Character clothing");
            node.transform.SetParent(_root, false);
            var renderer = node.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = _bones;
            renderer.localBounds = new Bounds(Vector3.up * 0.85f, new Vector3(3.6f, 2.8f, 3.6f));
            var material = Resources.Load<Material>("Materials/Plain");
            renderer.sharedMaterials = colors.Select(color =>
            {
                var instance = new Material(material);
                instance.SetColor("_BaseColor", color);
                instance.SetFloat("_Smoothness", 0.15f);
                return instance;
            }).ToArray();
        }
    }

    internal static class CharacterBones
    {
        public static Dictionary<string, Transform> ToDictionarySafe(this Transform[] bones)
        {
            var names = new Dictionary<string, Transform>();
            foreach (Transform bone in bones) if (!names.ContainsKey(bone.name)) names.Add(bone.name, bone);
            return names;
        }
    }
}
