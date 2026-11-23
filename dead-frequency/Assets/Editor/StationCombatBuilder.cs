using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        static void CreateCombatAssets(SerializedObject game, Camera camera)
        {
            var flash = Surface("Weapon - muzzle flash", new Color(1f, 0.58f, 0.22f), 0.15f, emission: 290);
            var weapon = Pivot(camera.transform, "Service pistol viewmodel", new(0.143f, -0.196f, 0.510f));
            var magazine = InstallPistol(weapon, out Transform slide, out Vector3 muzzlePoint);
            var supportHand = InstallArms(weapon, new(0.19f, -0.22f, -0.42f), new(0.020f, -0.054f, -0.067f),
                new(-0.28f, -0.24f, -0.40f), new(-0.042f, -0.105f, -0.036f));
            weapon.localRotation = Quaternion.Euler(0f, -10f, 6f);
            var audio = camera.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = 0.55f;
            game.FindProperty("weaponAudio").objectReferenceValue = audio;
            CreateArsenal(game, camera, flash, weapon, magazine, slide, supportHand, MuzzleBurst(weapon, muzzlePoint, flash));
            game.FindProperty("reloadSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Reload.wav");
            game.FindProperty("signalSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Radio.wav");
            game.FindProperty("infectedPrefab").objectReferenceValue = InfectedPrefab();
            game.FindProperty("bossPrefab").objectReferenceValue = BuildYardBoss();
            var civilians = CivilianPrefabs();
            var commons = game.FindProperty("commonPrefabs");
            commons.arraySize = civilians.Length;
            for (int index = 0; index < civilians.Length; index++)
                commons.GetArrayElementAtIndex(index).objectReferenceValue = civilians[index];
            game.FindProperty("shoveSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Shove.wav");
            game.FindProperty("fleshSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Flesh_Hit.wav");
            game.FindProperty("hordeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/Horde_Scream.wav");
        }

        static Transform InstallPistol(Transform weapon, out Transform slide, out Vector3 muzzlePoint)
        {
            const string sourcePath = "Assets/Art/Models/ServicePistol/";
            var material = Surface("Weapon - service pistol PBR", new Color(0.88f, 0.87f, 0.84f), 0.61f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath + "BaseColor.jpg"));
            material.SetTexture("_NormalMap", ImportNormal(sourcePath + "Normal.jpg"));
            var mask = (TextureImporter)AssetImporter.GetAtPath(sourcePath + "Mask.png");
            if (mask.sRGBTexture)
            {
                mask.sRGBTexture = false;
                mask.SaveAndReimport();
            }
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath + "Mask.png"));
            material.SetFloat("_NormalScale", 0.72f);
            material.SetFloat("_SmoothnessRemapMax", 0.77f);
            material.SetFloat("_MetallicRemapMax", 0.92f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);

            var pistol = ImportModel(sourcePath + "service_pistol.fbx", weapon, material);
            Transform[] pieces = pistol.GetComponentsInChildren<Transform>();
            Transform trigger = pieces.First(node => node.name.EndsWith("trigger_a"));
            Transform hammer = pieces.First(node => node.name.EndsWith("hammer_a"));
            slide = pieces.First(node => node.name.EndsWith("slide_a"));
            Transform magazine = pieces.First(node => node.name.EndsWith("magazine_loaded"));
            foreach (Transform node in pieces)
            {
                if (node.name.EndsWith("_b") || node.name.EndsWith("magazine_empty") || node.name.EndsWith("_bullet"))
                    node.gameObject.SetActive(false);
            }

            Vector3 direction = Vector3.ProjectOnPlane(trigger.position - hammer.position, Vector3.up);
            pistol.transform.rotation = Quaternion.FromToRotation(direction, weapon.forward) * pistol.transform.rotation;
            pistol.transform.position += weapon.TransformPoint(new Vector3(0f, -0.018f, 0.012f)) - trigger.position;
            magazine.position = trigger.position + weapon.TransformDirection(new Vector3(0f, -0.048f, -0.038f));

            var slideBounds = slide.GetComponent<Renderer>().bounds;
            muzzlePoint = weapon.InverseTransformPoint(new Vector3(
                slideBounds.center.x, slideBounds.center.y, slideBounds.max.z + 0.026f));
            return magazine;
        }

        static Transform InstallArms(Transform weapon, Vector3 rightShoulder, Vector3 rightGrip,
            Vector3 leftShoulder, Vector3 leftGrip, bool longGun = false)
        {
            const string armsPath = "Assets/Art/Models/FirstPersonArms/";
            var material = Surface("Equipment - rigged rescue arms", Color.white, 0.24f);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(armsPath + "Gloves.png"));
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);

            var arms = ImportModel(armsPath + "Arms.fbx", weapon, material);
            var armBones = arms.GetComponentsInChildren<Transform>();
            Transform rightUpper = armBones.First(bone => bone.name == "upper_arm.R");
            Transform rightLower = armBones.First(bone => bone.name == "forearm.R");
            Transform rightHand = armBones.First(bone => bone.name == "hand.R");
            Transform leftUpper = armBones.First(bone => bone.name == "upper_arm.L");
            Transform leftLower = armBones.First(bone => bone.name == "forearm.L");
            Transform leftHand = armBones.First(bone => bone.name == "hand.L");
            float armLength = Vector3.Distance(rightUpper.position, rightLower.position) +
                Vector3.Distance(rightLower.position, rightHand.position);
            arms.transform.localScale *= 0.57f / Mathf.Max(0.01f, armLength);
            PoseArm(weapon, rightUpper, rightLower, rightHand, rightShoulder, rightGrip, new(0.46f, -1f, 0), armBones, "R");
            PoseArm(weapon, leftUpper, leftLower, leftHand, leftShoulder, leftGrip, new(-0.8f, -1f, 0), armBones, "L", longGun);
            return leftHand;
        }

        static void PoseArm(Transform weapon, Transform upper, Transform forearm, Transform hand,
            Vector3 shoulder, Vector3 grip, Vector3 elbowBias, Transform[] bones, string side, bool underHandguard = false)
        {
            float upperLength = Vector3.Distance(upper.position, forearm.position);
            float lowerLength = Vector3.Distance(forearm.position, hand.position);
            upper.position = weapon.TransformPoint(shoulder);
            Vector3 toward = weapon.TransformPoint(grip) - upper.position;
            Vector3 line = toward.normalized;
            float reach = Mathf.Clamp(toward.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.004f,
                upperLength + lowerLength - 0.004f);
            Vector3 bend = Vector3.ProjectOnPlane(weapon.TransformDirection(elbowBias), line).normalized;
            float jointAlong = (upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2 * reach);
            float jointAcross = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - jointAlong * jointAlong));
            Vector3 elbow = upper.position + line * jointAlong + bend * jointAcross;
            upper.rotation = Quaternion.FromToRotation(forearm.position - upper.position, elbow - upper.position) * upper.rotation;
            Vector3 goal = upper.position + line * reach;
            Transform tip = bones.First(bone => bone.name == $"forearm.{side}_end");
            forearm.rotation = Quaternion.FromToRotation(tip.position - forearm.position, goal - forearm.position) * forearm.rotation;
            hand.position = goal;
            Transform middle = bones.First(bone => bone.name == $"f_middle.01.{side}");
            Transform index = bones.First(bone => bone.name == $"f_index.01.{side}");
            Transform pinky = bones.First(bone => bone.name == $"f_pinky.01.{side}");
            Vector3 originalForward = (middle.position - hand.position).normalized;
            Vector3 originalNormal = Vector3.Cross(originalForward, index.position - pinky.position).normalized;
            bool left = side == "L";
            Vector3 gripForward = weapon.TransformDirection(underHandguard ? new Vector3(0.78f, 0.5f, 0.3f) :
                left ? new Vector3(0.72f, -0.39f, 0.57f) : new Vector3(0, -0.81f, 0.58f));
            Vector3 gripNormal = weapon.TransformDirection(underHandguard ? new Vector3(0.25f, -0.95f, 0f) :
                left ? new Vector3(0.83f, 0.55f, 0f) : Vector3.left);
            hand.rotation = Quaternion.LookRotation(gripForward, gripNormal) *
                Quaternion.Inverse(Quaternion.LookRotation(originalForward, originalNormal)) * hand.rotation;
            foreach (string digit in new[] { "index", "middle", "ring", "pinky" })
            for (int joint = 1; joint <= 2; joint++)
            {
                Transform finger = bones.First(bone => bone.name == $"f_{digit}.0{joint}.{side}");
                Transform next = bones.First(bone => bone.name == $"f_{digit}.0{joint + 1}.{side}");
                Vector3 curlAxis = Vector3.Cross(next.position - finger.position, gripNormal);
                float angle = left ? (joint == 1 ? 45f : 71f) : (digit == "index" ? 20f : 48f);
                finger.rotation = Quaternion.AngleAxis(angle, curlAxis) * finger.rotation;
            }
        }

        const string InfectedFolder = "Assets/Art/Models/Infected/";

        static StationInfected InfectedPrefab()
        {
            var suit = Surface("Infected - imported skin", new Color(0.98f, 0.99f, 0.93f), 0.27f);
            suit.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(InfectedFolder + "difusse.jpg"));
            suit.SetTexture("_NormalMap", ImportNormal(InfectedFolder + "normal.JPG"));
            suit.SetFloat("_NormalScale", 0.67f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(suit);
            EditorUtility.SetDirty(suit);
            var walk = ImportLegacyClip(InfectedFolder + "walk.FBX");
            var attack = ImportLegacyClip(InfectedFolder + "fury.FBX");

            var actor = new GameObject("Infected");
            var body = actor.AddComponent<CharacterController>();
            body.height = 1.78f;
            body.radius = 0.34f;
            body.center = new Vector3(0, 0.89f, 0);
            body.stepOffset = 0.17f;
            actor.AddComponent<Unity.Netcode.NetworkObject>();
            var infected = actor.AddComponent<StationInfected>();
            actor.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            var voice = actor.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.minDistance = 1.5f;
            voice.maxDistance = 13f;
            voice.rolloffMode = AudioRolloffMode.Logarithmic;
            var rig = ImportModel(InfectedFolder + "walk.FBX", actor.transform, suit);
            FitModel(rig, 1.76f);
            rig.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var player = rig.GetComponent<Animation>();
            if (player == null)
                player = rig.AddComponent<Animation>();
            player.playAutomatically = false;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            var data = new SerializedObject(infected);
            data.FindProperty("rig").objectReferenceValue = player;
            data.FindProperty("warningAudio").objectReferenceValue = voice;
            data.FindProperty("attackCue").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/InfectedWarning.wav");
            data.FindProperty("walkClip").objectReferenceValue = walk;
            data.FindProperty("attackClip").objectReferenceValue = attack;
            SetClips(data, "groans", Clips("Zombie_Groan"));
            SetClips(data, "screams", Clips("Zombie_Scream"));
            data.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.Refresh();
            var prefab = PrefabUtility.SaveAsPrefabAsset(actor, "Assets/Prefabs/InfectedWorker.prefab");
            Object.DestroyImmediate(actor);
            return prefab.GetComponent<StationInfected>();
        }

        static StationInfected BuildYardBoss()
        {
            const string path = "Assets/Art/Models/GiantMutant/GiantMutant.glb";
            var importer = AssetImporter.GetAtPath(path);
            var settings = new SerializedObject(importer);
            var animationMethod = settings.FindProperty("importSettings.animationMethod");
            int legacy = System.Array.IndexOf(animationMethod.enumNames, "Legacy");
            if (animationMethod.enumValueIndex != legacy)
            {
                animationMethod.enumValueIndex = legacy;
                settings.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
            AnimationClip FindClip(string name) => clips.First(clip => clip.name == name);
            var actor = new GameObject("Loading yard mutant", typeof(Unity.Netcode.NetworkObject),
                typeof(Unity.Netcode.Components.NetworkTransform), typeof(CharacterController), typeof(AudioSource), typeof(StationInfected));
            var body = actor.GetComponent<CharacterController>();
            body.height = 1.95f;
            body.radius = 0.43f;
            body.center = new Vector3(0, 0.975f, 0);
            body.stepOffset = 0.22f;
            var voice = actor.GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.minDistance = 3f;
            voice.maxDistance = 26f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), actor.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            FitModel(model, 2.0f);
            var skin = Surface("Loading mutant - matte skin", new Color(0.91f, 0.88f, 0.80f), 0.24f);
            skin.SetTexture("_BaseColorMap", AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().First());
            skin.SetFloat("_Metallic", 0.02f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(skin);
            EditorUtility.SetDirty(skin);
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterial = skin;
            var rig = model.GetComponentInChildren<Animation>();
            rig.playAutomatically = false;
            rig.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.updateWhenOffscreen = true;
            var data = new SerializedObject(actor.GetComponent<StationInfected>());
            data.FindProperty("rig").objectReferenceValue = rig;
            data.FindProperty("warningAudio").objectReferenceValue = voice;
            data.FindProperty("attackCue").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/InfectedWarning.wav");
            data.FindProperty("walkClip").objectReferenceValue = FindClip("Giant Run");
            data.FindProperty("idleClip").objectReferenceValue = FindClip("idle");
            data.FindProperty("attackClip").objectReferenceValue = FindClip("Right Punch");
            data.FindProperty("bossSlamClip").objectReferenceValue = FindClip("Jump Slam");
            data.FindProperty("bespokeBoss").boolValue = true;
            SetClips(data, "groans", Clips("Zombie_Groan"));
            SetClips(data, "screams", Clips("Zombie_Scream"));
            data.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(actor, "Assets/Prefabs/LoadingYardMutant.prefab");
            Object.DestroyImmediate(actor);
            return prefab.GetComponent<StationInfected>();
        }

        static AnimationClip ImportLegacyClip(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.animationType != ModelImporterAnimationType.Legacy)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(clip => !clip.name.StartsWith("__preview__"));
        }

        static Texture2D ImportNormal(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static GameObject ImportModel(string path, Transform parent, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = material;
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }
            return instance;
        }

        static void FitModel(GameObject model, float height)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            float scale = height / Mathf.Max(bounds.size.y, 0.001f);
            model.transform.localScale *= scale;
            model.transform.position += new Vector3(
                (model.transform.position.x - bounds.center.x) * scale,
                (model.transform.position.y - bounds.min.y) * scale,
                (model.transform.position.z - bounds.center.z) * scale);
        }

        static Transform Pivot(Transform parent, string label, Vector3 position)
        {
            var node = new GameObject(label).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        static void Detail(Transform parent, string label, Vector3 position, Vector3 size, Material material) =>
            Box(parent, label, position, size, material, false);

        static Transform Shape(Transform parent, PrimitiveType primitive, string label, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            return part.transform;
        }
    }
}
