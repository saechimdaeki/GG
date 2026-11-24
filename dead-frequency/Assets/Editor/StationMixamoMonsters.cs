using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Special infected and the loading-bay boss on Mixamo bodies: 덮치개 Prisoner, 끌개 Skeletonzombie,
    /// 부풀이 Pumpkinhulk, 울음꾼 Girlscout, and the boss on Mutant with the Creature Pack. Specials keep their
    /// Resources path (and network prefab GUIDs); the boss prefab keeps its scene and network wiring.
    /// Batch: -executeMethod ResidualSignal.StationSceneBuilder.InstallMixamoMonsters
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const string MonsterRoot = MixamoRoot + "/Monsters";

        [MenuItem("잔류신호/특수 감염체·보스 Mixamo 교체")]
        public static void InstallMixamoMonsters()
        {
            foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { MixamoRoot + "/Animations/Creature" }).Select(AssetDatabase.GUIDToAssetPath))
                PrepareMixamoImport(path);

            Special("Leaper", "Prisoner", 1.8f, ("NotSoScary", "zombie idle (2)"), ("Scary", "zombie walk"), ("Scary", "zombie run"),
                ("NotSoScary", "zombie attack"));
            Special("Dragger", "Skeletonzombie", 1.95f, ("NotSoScary", "zombie scratch idle"), ("NotSoScary", "walking"),
                ("NotSoScary", "zombie running"), ("NotSoScary", "zombie punching"));
            Special("Bloater", "Pumpkinhulk", 1.75f, ("NotSoScary", "zombie agonizing"), ("NotSoScary", "zombie stumbling"),
                ("NotSoScary", "zombie running"), ("NotSoScary", "zombie headbutt"));
            Special("Caller", "Girlscout", 1.55f, ("NotSoScary", "zombie idle (3)"), ("NotSoScary", "walking (2)"),
                ("Scary", "zombie run"), ("Scary", "zombie attack"), scream: true);
            Boss();
            AssetDatabase.SaveAssets();
            Debug.Log("Mixamo specials and boss installed.");
        }

        static void Special(string kind, string body, float height, (string pack, string file) idle, (string pack, string file) walk,
            (string pack, string file) run, (string pack, string file) attack, bool scream = false)
        {
            var model = BuildMixamoModel(MonsterRoot, body, height, $"Assets/Prefabs/Monster{body}.prefab", "Monster");
            string path = $"{AlternateResources}/{kind}.prefab";
            var actor = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var infected = new SerializedObject(actor.GetComponent<StationInfected>());
                var rig = SwapActorModel(actor, infected, model);
                infected.FindProperty("rig").objectReferenceValue = rig;
                infected.FindProperty("idleClip").objectReferenceValue = MixamoClip(idle.pack, idle.file);
                infected.FindProperty("walkClip").objectReferenceValue = MixamoClip(walk.pack, walk.file);
                infected.FindProperty("runClip").objectReferenceValue = MixamoClip(run.pack, run.file);
                infected.FindProperty("attackClip").objectReferenceValue = MixamoClip(attack.pack, attack.file);
                infected.FindProperty("hitClip").objectReferenceValue = MixamoClip("NotSoScary", "zombie reaction hit");
                if (scream)
                    infected.FindProperty("screamClip").objectReferenceValue = MixamoClip("Scary", "zombie scream");
                infected.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(actor, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }
        }

        static void Boss()
        {
            var model = BuildMixamoModel(MonsterRoot, "Mutant", 2.05f, "Assets/Prefabs/MonsterMutant.prefab", "Monster");
            const string path = "Assets/Prefabs/LoadingYardMutant.prefab";
            var actor = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var infected = new SerializedObject(actor.GetComponent<StationInfected>());
                var rig = SwapActorModel(actor, infected, model);
                infected.FindProperty("rig").objectReferenceValue = rig;
                infected.FindProperty("idleClip").objectReferenceValue = MixamoClip("Creature", "mutant breathing idle");
                infected.FindProperty("walkClip").objectReferenceValue = MixamoClip("Creature", "mutant run");
                infected.FindProperty("attackClip").objectReferenceValue = MixamoClip("Creature", "mutant swiping");
                infected.FindProperty("bossSlamClip").objectReferenceValue = MixamoClip("Creature", "mutant jump attack");
                infected.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(actor, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }
        }

        /// <summary>Removes the actor's current body (the rig's top-level child) and parents a fresh instance of the model.</summary>
        static Animation SwapActorModel(GameObject actor, SerializedObject infected, GameObject model)
        {
            var oldRig = (Animation)infected.FindProperty("rig").objectReferenceValue;
            if (oldRig != null)
            {
                Transform old = oldRig.transform;
                while (old.parent != actor.transform)
                    old = old.parent;
                Object.DestroyImmediate(old.gameObject);
            }
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, actor.transform);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            return body.GetComponent<Animation>();
        }
    }
}
