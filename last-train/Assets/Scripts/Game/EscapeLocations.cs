using UnityEngine;
using UnityEngine.Rendering;

namespace LastTrain
{
    public partial class City
    {
        internal static readonly Vector2 WestGate = new(-35f, 7f);
        internal static readonly Vector2 DrainExit = new(7f, -35f);
        internal static readonly Vector2 DrainContact = new(7f, -21f);
        internal static readonly Vector2 MarketContact = MarketSquare + new Vector2(0, 3.5f);
        internal static readonly Vector2[] DrainValves =
        {
            DrainExit + new Vector2(-2.4f, -1.85f),
            DrainExit + new Vector2(2.4f, 2.15f),
        };
        public Renderer RoadRing, DrainRing;
        readonly Renderer[] _valveIndicators = new Renderer[2];

        bool BuildEscapeLocation(Vector2 cell)
        {
            if (cell == WestGate) BuildCheckpoint();
            else if (cell == DrainExit) BuildDrain();
            else if (cell == DrainContact) BuildMaintenanceContact();
            else return false;
            return true;
        }

        static GameObject LoadHollowsProp(string name) => Resources.Load<GameObject>("Art/Hollows/Prefabs/" + name);

        GameObject FieldProp(string name, string fallback)
        {
            var prefab = LoadHollowsProp(name);
            return prefab ? Instantiate(prefab, transform) : Model("Art/Field/" + fallback);
        }

        Material SiteMaterial(Color color)
        {
            var material = new Material(Resources.Load<Material>("Materials/Concrete"));
            material.SetColor("_BaseColor", color);
            return material;
        }

        Renderer EscapeRing(Vector2 center, float radius)
        {
            var node = new GameObject("Escape zone");
            node.transform.SetParent(transform, false);
            node.transform.position = Fx.V3(center, 0.12f);
            node.transform.localScale = Vector3.one * radius;
            node.AddComponent<MeshFilter>().sharedMesh = Fx.BuildRing(0.96f, 1f, 72);
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Resources.Load<Material>("Materials/FxAdd"));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false;
            return renderer;
        }

        void SiteLamp(Vector2 at, float height, Color color)
        {
            var light = new GameObject("Route lamp").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.position = Fx.V3(at, height);
            light.type = LightType.Point;
            light.color = color;
            light.range = 12f;
            light.intensity = 4.2f;
            Block(light.transform.position, new Vector3(0.43f, 0.22f, 0.39f), Resources.Load<Material>("Materials/Bulb"));
            StreetLights.Add(light);
        }

        void BuildCheckpoint()
        {
            Vector2 at = WestGate;
            var asphalt = SiteMaterial(new Color(0.31f, 0.33f, 0.33f));
            var yellow = SiteMaterial(new Color(0.81f, 0.58f, 0.24f));
            var paintedMetal = SiteMaterial(new Color(0.40f, 0.44f, 0.32f));
            var darkMetal = SiteMaterial(new Color(0.14f, 0.18f, 0.19f));
            Block(Fx.V3(at, 0.027f), new Vector3(11.4f, 0.05f, 10.7f), asphalt);

            var ends = new[] { -1, 1 };
            var lines = new SurfaceBatch();
            foreach (int side in ends)
            {
                lines.Add(at + new Vector2(side * 3.4f, 0), new Vector2(0.12f, 9f), 0.055f);
                for (int stripe = 0; stripe < 4; stripe++)
                    lines.Add(at + new Vector2(side * (3.7f + stripe * 0.38f), -2.5f), new Vector2(0.16f, 2.7f), 0.055f);
                Fit(Model("Art/Roads/construction-barrier"), at + new Vector2(side * 4.4f, -2.9f), 2.1f, 1.2f, 0);
                Fit(Model("Art/Roads/construction-fence"), at + new Vector2(side * 5.1f, 2.2f), 3.1f, 2.25f, 90);
            }
            lines.Create("Checkpoint road paint", transform, yellow);

            // Park the vehicle clear of the marked boarding zone.
            Vector2 van = at + new Vector2(0, 5.2f);
            var vehicle = LoadHollowsProp("box_body_ambulance");
            if (vehicle)
            {
                var instance = Instantiate(vehicle, transform);
                Bounds original = BoundsOf(instance);
                Fit(instance, van, 5.0f, 2.65f, original.size.x > original.size.z ? 90f : 0f);
                Bounds parked = BoundsOf(instance);
                Walls.Add(new Rect(parked.min.x, parked.min.z, parked.size.x, parked.size.z));
            }
            else
            {
                Block(Fx.V3(van, 0.60f), new Vector3(2.5f, 0.42f, 4.7f), darkMetal);
                Block(Fx.V3(van + Vector2.up * 1.22f, 1.26f), new Vector3(2.45f, 1.57f, 1.65f), paintedMetal);
                Block(Fx.V3(van + Vector2.up * 2.06f, 1.59f), new Vector3(1.93f, 0.51f, 0.04f), darkMetal);
                Block(Fx.V3(van + Vector2.down * 0.65f, 0.94f), new Vector3(2.5f, 0.24f, 2.6f), paintedMetal);
                foreach (int side in ends)
                {
                    Block(Fx.V3(van + new Vector2(side * 1.2f, -0.65f), 1.14f), new Vector3(0.15f, 0.32f, 2.6f), darkMetal);
                    foreach (int row in ends)
                    {
                        var tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        Destroy(tire.GetComponent<Collider>());
                        tire.transform.SetParent(transform, false);
                        tire.transform.SetPositionAndRotation(Fx.V3(van + new Vector2(side * 1.19f, row * 1.37f), 0.46f), Quaternion.Euler(0, 0, 90));
                        tire.transform.localScale = new Vector3(0.86f, 0.19f, 0.86f);
                        tire.GetComponent<Renderer>().sharedMaterial = darkMetal;
                    }
                }
                Walls.Add(new Rect(van.x - 1.4f, van.y - 2.3f, 2.8f, 4.55f));
            }

            var sign = Fx.V3(at + new Vector2(-4.3f, 3.0f), 1.16f);
            Block(sign, new Vector3(3f, 0.94f, 0.19f), darkMetal);
            SignFace("west-gate", sign + Vector3.back * 0.098f, Vector3.back, 2.96f, 0.87f);
            Fit(Haven("cardboard_box_01", Vector3.zero, 0), at + new Vector2(4.5f, 3.1f), 1.05f, 1.05f, 15);
            SiteLamp(at + new Vector2(-3.2f, 2.6f), 2.8f, new Color(1f, 0.68f, 0.36f));
            RoadRing = EscapeRing(at, Tuning.Data.clock.evacRadius);
        }

        void BuildDrain()
        {
            Vector2 at = DrainExit;
            var concrete = SiteMaterial(new Color(0.50f, 0.54f, 0.53f));
            var water = SiteMaterial(new Color(0.075f, 0.23f, 0.24f));
            water.SetFloat("_Smoothness", 0.75f);
            var grate = SiteMaterial(new Color(0.25f, 0.34f, 0.36f));

            Block(Fx.V3(at, 0.025f), new Vector3(8.4f, 0.05f, 10.9f), concrete);
            Block(Fx.V3(at + Vector2.down * 3.6f, 0.068f), new Vector3(4.55f, 0.025f, 3.7f), water);
            Block(Fx.V3(at + Vector2.up * 3.7f, 0.97f), new Vector3(5.1f, 1.9f, 0.55f), concrete);
            Walls.Add(new Rect(at.x - 2.55f, at.y + 3.43f, 5.1f, 0.55f));
            for (int i = -4; i <= 4; i++)
                Block(Fx.V3(at + new Vector2(i * 0.48f, 3.36f), 0.91f), new Vector3(0.13f, 1.71f, 0.17f), grate);
            foreach (int side in new[] { -1, 1 })
            {
                Vector2 edge = at + new Vector2(side * 3.3f, -0.8f);
                Block(Fx.V3(edge, 0.37f), new Vector3(0.44f, 0.7f, 6.7f), concrete);
                Walls.Add(new Rect(edge.x - 0.22f, edge.y - 3.35f, 0.44f, 6.7f));
            }

            var sign = Fx.V3(at + Vector2.up * 3.37f, 1.78f);
            SignFace("drain-exit", sign, Vector3.back, 3.52f, 0.63f);
            SiteLamp(at + new Vector2(1.9f, 2.65f), 2.25f, new Color(0.35f, 0.80f, 1f));
            DrainRing = EscapeRing(at, Tuning.Data.escape.drainRadius);
            for (int i = 0; i < DrainValves.Length; i++)
            {
                Vector2 point = DrainValves[i];
                Block(Fx.V3(point, 0.46f), new Vector3(0.48f, 0.92f, 0.42f), grate);
                _valveIndicators[i] = Block(Fx.V3(point, 0.98f), new Vector3(0.34f, 0.13f, 0.22f), _neon).GetComponent<Renderer>();
                SetValveIndicator(i, false);
            }
        }

        internal void SetValveIndicator(int valve, bool open)
        {
            Color color = open ? new Color(0.33f, 1f, 0.63f) : new Color(1f, 0.30f, 0.16f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 1.8f);
            _valveIndicators[valve].SetPropertyBlock(block);
        }

        void BuildMaintenanceContact()
        {
            var at = DrainContact;
            var surface = SiteMaterial(new Color(0.27f, 0.38f, 0.34f));
            Block(Fx.V3(at, 0.028f), new Vector3(7.4f, 0.053f, 6.1f), surface);
            var tableAt = at + new Vector2(2.1f, 0.6f);
            var desk = Fit(Haven("metal_office_desk", Vector3.zero, 0), tableAt, 1.6f, 0.95f, 90);
            var radio = Fit(FieldProp("valve_radio", "radio"), tableAt, 0.65f, 0.45f, 0);
            radio.transform.position += Vector3.up * BoundsOf(desk).max.y;
            Fit(Model("Art/Roads/construction-fence"), at + new Vector2(-2.5f, 2.5f), 3.1f, 1.9f, 0);
            var sign = Fx.V3(at + new Vector2(0.7f, 2.5f), 1.38f);
            Block(sign, new Vector3(2.8f, 0.85f, 0.2f), surface);
            SignFace("maintenance", sign + Vector3.back * 0.104f, Vector3.back, 2.77f, 0.78f);
            SiteLamp(at + new Vector2(-2, 1), 2.3f, new Color(0.56f, 0.94f, 0.66f));
        }
    }
}
