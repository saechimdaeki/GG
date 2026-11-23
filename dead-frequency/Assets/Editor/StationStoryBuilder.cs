using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ResidualSignal
{
    public static partial class StationSceneBuilder
    {
        const string PayphoneFolder = "Assets/Art/Models/Payphone/korean_public_payphone_01_";

        static void CreateStory(Transform station, SerializedObject game)
        {
            var steel = Surface("Story - station cabinet steel", new Color(0.30f, 0.33f, 0.32f), 0.44f);
            var rubber = Surface("Story - equipment rubber", new Color(0.17f, 0.17f, 0.155f), 0.23f);
            var paper = Surface("Story - register paper", new Color(0.85f, 0.82f, 0.67f), 0.30f);
            var signal = Surface("Story - signal green", new Color(0.35f, 0.83f, 0.57f), 0.34f, emission: 26f);
            var read = Surface("Story - red stamp", new Color(0.51f, 0.12f, 0.088f), 0.25f);
            var tan = Surface("Story - canvas supply box", new Color(0.53f, 0.47f, 0.29f), 0.32f);
            var phoneMaterial = Surface("Story - Korean public payphone", new Color(0.86f, 0.88f, 0.84f), 0.45f);
            phoneMaterial.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PayphoneFolder + "diff_1k.jpg"));
            phoneMaterial.SetTexture("_NormalMap", ImportNormal(PayphoneFolder + "nor_gl_1k.exr"));
            phoneMaterial.SetFloat("_NormalScale", 0.65f);
            HDMaterial.ValidateMaterial(phoneMaterial);
            EditorUtility.SetDirty(phoneMaterial);

            var radio = Pivot(station, "Emergency phone", new(-3.95f, 0, 1.6f));
            var phone = ImportModel(PayphoneFolder + "1k.fbx", radio, phoneMaterial);
            FitModel(phone, 0.52f);
            phone.transform.localPosition += Vector3.up * 0.96f;
            phone.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Box(radio, "Call pedestal", new(0, 0.48f, 0), new(0.31f, 0.96f, 0.34f), steel);
            Box(radio, "Emergency line lamp", new(0, 1.54f, -0.08f), new(0.10f, 0.055f, 0.067f), signal, false);
            StationLight(station, new(-4.23f, 1.97f, 1.69f), new Color(0.66f, 1f, 0.80f), 1700, 4.3f);

            var roster = Pivot(station, "Evacuation register", new(-3.85f, 0, 6.6f));
            Box(roster, "Station desk", new(0, 0.48f, 0.11f), new(1.18f, 0.96f, 0.57f), steel);
            Box(roster, "Register cover", new(0.10f, 0.981f, -0.015f), new(0.33f, 0.025f, 0.28f), rubber, false);
            Box(roster, "Open ledger page", new(0.10f, 0.999f, -0.015f), new(0.30f, 0.013f, 0.25f), paper, false);
            for (int row = 0; row < 6; row++)
            {
                Box(roster, "Handwritten roster row", new(0.055f, 1.006f, -0.11f + row * 0.033f),
                    new(0.15f, 0.002f, 0.005f), rubber, false);
                Box(roster, "Evacuated stamp", new(0.18f, 1.006f, -0.11f + row * 0.033f),
                    new(0.052f, 0.002f, 0.015f), read, false);
            }
            Box(roster, "Desk drawer pull", new(-0.18f, 0.79f, -0.187f), new(0.29f, 0.044f, 0.056f), rubber, false);

            Spotlight(station, "Work light on open register", new(-4.35f, 2.7f, 5.80f),
                new(-3.75f, 0.99f, 6.6f), new Color(1f, 0.87f, 0.67f), 950f, 56f, 5.8f);

            var supply = Pivot(station, "Station supply case", new(4.77f, 0, 10.1f));
            Box(supply, "Canvas case", new(0, 0.14f, 0), new(0.55f, 0.28f, 0.42f), tan);
            Box(supply, "Case seal", new(0, 0.28f, 0), new(0.57f, 0.04f, 0.43f), rubber, false);
            Box(supply, "First-aid cross vertical", new(0, 0.31f, 0), new(0.05f, 0.01f, 0.19f), paper, false);
            Box(supply, "First-aid cross horizontal", new(0, 0.31f, 0), new(0.19f, 0.01f, 0.05f), paper, false);

            var witness = Pivot(station, "Seo I-gyeong", new(1.55f, 0, 33.4f));
            witness.localRotation = Quaternion.Euler(0, 180f, 0);
            var witnessSettings = new SerializedObject(witness.gameObject.AddComponent<StationWitness>());
            witnessSettings.FindProperty("rig").objectReferenceValue = InstallWitnessAvatar(witness);
            witnessSettings.ApplyModifiedPropertiesWithoutUndo();
            var lift = Pivot(station, "Service lift", new(-1.6f, 0, 35.1f));
            Box(lift, "Lift threshold", new(0, 0.011f, 0), new(2.15f, 0.02f, 1.35f), steel, false);
            Box(lift, "Service lift beacon", new(0, 2.94f, 0.64f), new(0.28f, 0.10f, 0.067f), signal, false);
            StationLight(station, new(-1.6f, 2.30f, 35.2f), new Color(0.60f, 0.98f, 0.79f), 500, 5f);

            game.FindProperty("radio").objectReferenceValue = radio;
            game.FindProperty("roster").objectReferenceValue = roster;
            game.FindProperty("supply").objectReferenceValue = supply;
            game.FindProperty("witness").objectReferenceValue = witness.GetComponent<StationWitness>();
            game.FindProperty("evacuationPoint").objectReferenceValue = lift;
        }
    }
}
