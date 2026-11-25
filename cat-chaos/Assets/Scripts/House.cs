using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatChaos
{
    /// <summary>
    /// The owner's flat, 12 x 10 m: living room and kitchen (south), bedroom, bathroom and
    /// entry hall (north). Kenney furniture, Poly Haven boxes and small shape-built things.
    /// Yaw 0 means a model's front faces +Z.
    /// </summary>
    public sealed class House
    {
        const float H = 2.6f, Wt = 0.15f;
        const float X0 = -6f, X1 = 6f, Z0 = -5f, Z1 = 5f;

        public readonly List<Prop> Props = new();
        public Vector3 CatStart = new(-1.4f, 0.05f, -2.4f);
        public Vector3 CatBed;
        public Transform Door;
        public Vector3 DoorEye = new(4.4f, 1.6f, 0.55f), DoorLook = new(-2.2f, 0.25f, -2.6f);
        public readonly List<Vector3> LampSpots = new();
        public Vector3 Dock = new(5.45f, 0, -3.3f);
        public float DockYaw = -90f;
        /// <summary>Behind the TV on its stand: where a cat stands to shove it off.</summary>
        public Vector3 TvSpot;
        /// <summary>Places a catnip sprig can turn up (floor, table tops, the fridge).</summary>
        public readonly List<Vector3> NipSpots = new();
        /// <summary>Open windows a butterfly can drift in through.</summary>
        public readonly Vector3[] WindowIn = { new(-4.6f, 1.5f, -4.7f), new(-5.7f, 1.5f, -2.2f), new(-3f, 1.5f, 4.7f), new(5.7f, 1.6f, -2.4f) };

        readonly Transform _root;
        readonly Material _wall, _trim, _frame;

        public House(Transform root)
        {
            _root = root;
            _wall = Mats.Saved("Plaster");
            _trim = Mats.Flat(new Color(0.93f, 0.91f, 0.86f), 0.4f);
            _frame = Mats.Flat(new Color(0.97f, 0.97f, 0.95f), 0.5f);
            Shell();
            Living();
            Kitchen();
            Bedroom();
            Bathroom();
            Entry();
            foreach (var (x, z) in new[] { (-1.0f, -3.4f), (-4.6f, -2.6f), (3.5f, -1.9f), (4.9f, -3.2f), (-1.6f, 2.3f), (-3.4f, 3.9f), (1.8f, 3.5f), (4.5f, 3.0f), (5.58f, -0.2f), (-5.0f, 1.3f) })
                NipSpots.Add(At(x, z));
        }

        // ================================================================== rooms
        void Shell()
        {
            Floor(new Rect(X0, Z0, 7, 6), "Planks");          // living
            Floor(new Rect(1, Z0, 5, 6), "FloorTile");         // kitchen
            Floor(new Rect(X0, 1, 6, 4), "PlanksLight");       // bedroom
            Floor(new Rect(0, 1, 3, 4), "BathTile");           // bathroom
            Floor(new Rect(3, 1, 3, 4), "Planks");             // entry

            // exterior: gaps are (center, width, sill, top)
            Wall(true, Z0, X0, X1, Win(-4.6f, 1.2f), Win(-0.8f, 1.2f), Win(2.84f, 1.4f, 1.1f));
            Wall(true, Z1, X0, X1, Win(-3f, 1.3f), Win(2.5f, 0.7f, 1.5f), Gap(4.5f, 1.0f));
            Wall(false, X0, Z0, Z1, Win(-2.2f, 1.4f), Win(3f, 1.1f));
            Wall(false, X1, Z0, Z1, Win(-2.4f, 1.2f, 1.1f));
            // interior
            Wall(true, 1f, X0, X1, Gap(-1f, 0.9f), Gap(2.4f, 0.8f), Gap(4.5f, 1.6f));
            Wall(false, 0f, 1f, Z1);
            Wall(false, 3f, 1f, Z1);

            var ceiling = Block("천장", new Vector3(0, H + 0.05f, 0), new Vector3(X1 - X0 + 0.4f, 0.1f, Z1 - Z0 + 0.4f), _trim);
            ceiling.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            var ground = Block("바깥", new Vector3(0, -0.2f, 0), new Vector3(40, 0.2f, 40), Mats.Flat(new Color(0.45f, 0.55f, 0.35f)));
            ground.GetComponent<MeshRenderer>().enabled = false;

            // front door, hinged on its left post
            var hinge = new GameObject("현관문").transform;
            hinge.SetParent(_root, false);
            hinge.localPosition = new Vector3(4.0f, 0, Z1 - 0.02f);
            var leaf = Block("문짝", new Vector3(0.5f, 1.05f, 0), new Vector3(1.0f, 2.1f, 0.06f), Mats.Flat(new Color(0.45f, 0.3f, 0.2f), 0.35f));
            leaf.transform.SetParent(hinge, false);
            var knob = Mats.Shape(PrimitiveType.Sphere, hinge, new Vector3(0.88f, 1.0f, -0.06f), Vector3.one * 0.06f, Mats.Flat(new Color(0.85f, 0.72f, 0.4f), 0.8f, 0.9f));
            knob.name = "손잡이";
            Door = hinge;
        }

        void Living()
        {
            Furn("rugRectangle", new Vector3(-2.6f, 0, -2.1f), 0, new Vector3(3.2f, 0.02f, 2.3f), solid: false);
            var sofa = Furn("loungeSofa", new Vector3(-2.6f, 0, 0.42f), 180, new Vector3(2.1f, 0.85f, 0.9f));
            sofa.AddComponent<Scratchable>().Label = "소파";
            Dyn("pillowBlue", new Vector3(-3.3f, 0.5f, 0.4f), 170, new Vector3(0.45f, 0.18f, 0.4f), "쿠션", PropKind.Thing, 0.3f);
            Dyn("pillow", new Vector3(-1.9f, 0.5f, 0.4f), 195, new Vector3(0.45f, 0.18f, 0.4f), "쿠션", PropKind.Thing, 0.3f);
            var table = Furn("tableCoffee", new Vector3(-2.6f, 0, -1.8f), 0, new Vector3(1.2f, 0.42f, 0.65f));
            // deep stand with the TV at its front edge: the cat hops up behind the TV and shoves it off
            Furn("cabinetTelevision", new Vector3(-2.6f, 0, -4.55f), 0, new Vector3(1.8f, 0.52f, 0.7f), keepAspect: false);
            var tvSet = Dyn("televisionModern", At(-2.6f, -4.33f), 0, new Vector3(1.1f, 0.7f, 0.16f), "TV", PropKind.Tv, 3.5f, big: true);
            Screen(tvSet.transform, new Vector3(1.1f, 0.7f, 0.16f));
            Dyn("speakerSmall", At(-3.38f, -4.32f), 0, new Vector3(0.16f, 0.28f, 0.16f), "스피커", PropKind.Thing, 1.2f);
            Dyn("speakerSmall", At(-1.82f, -4.32f), 0, new Vector3(0.16f, 0.28f, 0.16f), "스피커", PropKind.Thing, 1.2f);
            TvSpot = At(-2.6f, -4.72f);

            Cup(At(-2.9f, -1.7f), new Color(0.95f, 0.5f, 0.45f));
            Mug(At(-2.2f, -1.95f), new Color(0.35f, 0.55f, 0.85f));
            Remote(At(-2.55f, -1.6f));
            Bottle(At(-2.35f, -2.0f), new Color(0.3f, 0.5f, 0.8f));
            Vase(At(-3.05f, -1.95f), new Color(0.3f, 0.6f, 0.75f), new Color(1f, 0.5f, 0.6f));
            Dyn("plantSmall2", At(-2.0f, -1.6f), 0, new Vector3(0.2f, 0.3f, 0.2f), "작은 화분", PropKind.Plant, 0.6f);

            Furn("sideTable", new Vector3(-4.1f, 0, 0.45f), 180, new Vector3(0.5f, 0.55f, 0.5f));
            var tl = Dyn("lampRoundTable", At(-4.1f, 0.45f), 0, new Vector3(0.3f, 0.5f, 0.3f), "스탠드", PropKind.Thing, 1.2f, big: true);
            Bulb(tl.transform, 0.42f, 0.6f, 2.5f);
            Glass(At(-3.95f, 0.3f));

            var floorLamp = Dyn("lampRoundFloor", new Vector3(-0.85f, 0, 0.5f), 0, new Vector3(0.45f, 1.6f, 0.45f), "장스탠드", PropKind.Thing, 3f, big: true);
            Bulb(floorLamp.transform, 1.45f, 1.1f, 4f);
            var chair = Furn("loungeChair", new Vector3(-0.2f, 0, -2.4f), -100, new Vector3(0.9f, 0.85f, 0.9f));
            chair.AddComponent<Scratchable>().Label = "안락의자";

            var shelf = Furn("bookcaseOpen", new Vector3(X0 + 0.25f, 0, 0.0f), 90, new Vector3(0.38f, 1.85f, 1.1f));
            float[] shelves = ShelfHeights(shelf, X0 + 0.25f, 0.0f);
            for (int i = 0; i < shelves.Length; i++)
            {
                float y = shelves[i];
                if (i % 2 == 0)
                {
                    Dyn("books", new Vector3(X0 + 0.27f, y, -0.25f), 90, new Vector3(0.2f, 0.25f, 0.3f), "책", PropKind.Thing, 0.8f);
                    Frame(new Vector3(X0 + 0.25f, y, 0.25f), 90, new Color(0.9f, 0.75f, 0.5f));
                }
                else
                {
                    Dyn("plantSmall1", new Vector3(X0 + 0.27f, y, -0.3f), 0, new Vector3(0.18f, 0.25f, 0.18f), "작은 화분", PropKind.Plant, 0.5f);
                    Dyn("books", new Vector3(X0 + 0.27f, y, 0.2f), 90, new Vector3(0.2f, 0.25f, 0.3f), "책", PropKind.Thing, 0.8f);
                }
            }

            Dyn("pottedPlant", new Vector3(X0 + 0.5f, 0, Z0 + 0.5f), 0, new Vector3(0.6f, 1.15f, 0.6f), "큰 화분", PropKind.Plant, 4f, big: true);
            Dyn("plantSmall3", new Vector3(-4.6f, 0.82f, Z0 + 0.22f), 0, new Vector3(0.2f, 0.28f, 0.2f), "창가 화분", PropKind.Plant, 0.5f);
            Fishbowl(new Vector3(-4.1f, 0.81f, Z0 + 0.2f));
            Dyn("plantSmall1", new Vector3(-0.8f, 0.82f, Z0 + 0.22f), 0, new Vector3(0.2f, 0.28f, 0.2f), "창가 화분", PropKind.Plant, 0.5f);
            Yarn(new Vector3(-1.0f, 0.06f, -3.4f), new Color(0.95f, 0.35f, 0.4f));
            CatBed = new Vector3(0.45f, 0, Z0 + 0.6f);
            Bed(CatBed);
            DockBase();
        }

        void Kitchen()
        {
            float z = Z0 + 0.32f;
            string[] row = { "kitchenCabinetDrawer", "kitchenCabinet", "kitchenSink", "kitchenCabinet", "kitchenStove", "kitchenCabinetDrawer", "kitchenCabinet" };
            for (int i = 0; i < row.Length; i++)
                Furn(row[i], new Vector3(1.6f + i * 0.62f, 0, z), 0, new Vector3(0.62f, 0.92f, 0.62f), keepAspect: false);
            foreach (float x in new[] { 1.6f, 4.08f, 4.7f, 5.32f })
                Furn("kitchenCabinetUpper", new Vector3(x, 1.5f, Z0 + 0.2f), 0, new Vector3(0.62f, 0.7f, 0.35f), keepAspect: false);
            Furn("kitchenFridge", new Vector3(X1 - 0.42f, 0, -0.2f), -90, new Vector3(0.8f, 1.95f, 0.75f));

            Dyn("toaster", At(1.6f, z, 1.3f), 0, new Vector3(0.28f, 0.2f, 0.18f), "토스터", PropKind.Thing, 1.5f);
            Dyn("kitchenCoffeeMachine", At(2.22f, z - 0.05f, 1.3f), 0, new Vector3(0.25f, 0.35f, 0.25f), "커피머신", PropKind.Thing, 2f);
            Mug(At(2.45f, z + 0.12f, 1.3f), new Color(0.95f, 0.95f, 0.92f));
            Plates(At(3.5f, z, 1.3f));
            Glass(At(3.3f, z + 0.15f, 1.3f));
            Bottle(At(3.15f, z - 0.1f, 1.3f), new Color(0.2f, 0.45f, 0.25f));
            Bottle(At(3.95f, z - 0.12f, 1.3f), new Color(0.45f, 0.15f, 0.2f));
            Cup(At(3.75f, z + 0.12f, 1.3f), new Color(0.5f, 0.8f, 0.6f));
            Dyn("kitchenBlender", At(4.85f, z, 1.3f), 0, new Vector3(0.18f, 0.38f, 0.18f), "믹서기", PropKind.Thing, 1.5f);
            Dyn("kitchenMicrowave", At(5.32f, z - 0.03f, 1.3f), 0, new Vector3(0.5f, 0.3f, 0.38f), "전자레인지", PropKind.Thing, 4f, big: true);
            Bowl(At(4.4f, z + 0.05f, 1.3f));

            Furn("table", new Vector3(3.5f, 0, -1.9f), 0, new Vector3(1.4f, 0.76f, 0.85f));
            Dyn("chair", new Vector3(3.1f, 0, -2.65f), 0, new Vector3(0.45f, 0.95f, 0.45f), "의자", PropKind.Thing, 3f, big: true);
            Dyn("chair", new Vector3(3.9f, 0, -2.65f), 0, new Vector3(0.45f, 0.95f, 0.45f), "의자", PropKind.Thing, 3f, big: true);
            Dyn("chair", new Vector3(3.1f, 0, -1.15f), 180, new Vector3(0.45f, 0.95f, 0.45f), "의자", PropKind.Thing, 3f, big: true);
            Dyn("chair", new Vector3(3.9f, 0, -1.15f), 180, new Vector3(0.45f, 0.95f, 0.45f), "의자", PropKind.Thing, 3f, big: true);
            Plates(At(3.1f, -1.95f));
            Cup(At(3.35f, -1.7f), new Color(0.95f, 0.8f, 0.35f));
            Cup(At(3.9f, -2.05f), new Color(0.6f, 0.75f, 0.95f));
            Vase(At(3.6f, -1.85f), new Color(0.95f, 0.95f, 0.95f), new Color(1f, 0.85f, 0.3f));
            Bottle(At(3.4f, -2.2f), new Color(0.55f, 0.35f, 0.15f));
            Dyn("trashcan", new Vector3(X1 - 0.3f, 0, -1.2f), -90, new Vector3(0.35f, 0.5f, 0.35f), "쓰레기통", PropKind.Trash, 1.5f);
        }

        void Bedroom()
        {
            var bed = Furn("bedDouble", new Vector3(-3.4f, 0, Z1 - 1.08f), 180, new Vector3(1.7f, 0.9f, 2.1f));
            bed.AddComponent<Scratchable>().Label = "침대";
            Dyn("pillow", At(-3.85f, Z1 - 0.4f), 180, new Vector3(0.55f, 0.2f, 0.35f), "베개", PropKind.Thing, 0.4f);
            Dyn("pillow", At(-2.95f, Z1 - 0.4f), 180, new Vector3(0.55f, 0.2f, 0.35f), "베개", PropKind.Thing, 0.4f);
            Dyn("bear", At(-3.4f, Z1 - 0.9f), 200, new Vector3(0.3f, 0.35f, 0.25f), "곰인형", PropKind.Thing, 0.4f);
            foreach (float x in new[] { -4.65f, -2.15f })
            {
                Furn("cabinetBedDrawerTable", new Vector3(x, 0, Z1 - 0.3f), 180, new Vector3(0.5f, 0.55f, 0.45f));
                var lamp = Dyn("lampSquareTable", At(x, Z1 - 0.3f), 180, new Vector3(0.25f, 0.45f, 0.25f), "스탠드", PropKind.Thing, 1f, big: true);
                Bulb(lamp.transform, 0.38f, 0.5f, 2.2f);
            }
            Glass(At(-2.0f, Z1 - 0.38f));
            Clock(At(-4.75f, Z1 - 0.38f));

            Furn("desk", new Vector3(-5.0f, 0, 1.45f), 0, new Vector3(1.3f, 0.78f, 0.6f));
            Dyn("computerScreen", At(-5.0f, 1.3f), 0, new Vector3(0.55f, 0.4f, 0.18f), "모니터", PropKind.Thing, 3f, big: true);
            Dyn("computerKeyboard", At(-5.0f, 1.6f), 0, new Vector3(0.42f, 0.03f, 0.14f), "키보드", PropKind.Thing, 0.6f);
            Dyn("computerMouse", At(-4.62f, 1.62f), 0, new Vector3(0.06f, 0.03f, 0.1f), "마우스", PropKind.Thing, 0.1f);
            Mug(At(-5.48f, 1.6f), new Color(0.9f, 0.6f, 0.3f));
            Fishbowl(At(-5.45f, 1.3f));
            Dyn("radio", At(-4.5f, 1.35f), -20, new Vector3(0.3f, 0.2f, 0.12f), "라디오", PropKind.Thing, 0.8f);
            Dyn("chairDesk", new Vector3(-5.0f, 0, 2.15f), 180, new Vector3(0.55f, 0.95f, 0.55f), "의자", PropKind.Thing, 4f, big: true);
            Furn("bookcaseClosedDoors", new Vector3(-0.32f, 0, 2.2f), -90, new Vector3(0.45f, 1.9f, 1.0f));
            Dyn("cardboardBoxOpen", new Vector3(-0.35f, 1.9f, 2.2f), -90, new Vector3(0.4f, 0.3f, 0.35f), "상자", PropKind.Thing, 0.8f);
            Dyn("coatRackStanding", new Vector3(-1.0f, 0, Z1 - 0.35f), 0, new Vector3(0.45f, 1.7f, 0.45f), "옷걸이", PropKind.Thing, 3f, big: true);
            Furn("rugRound", new Vector3(-3.4f, 0, 1.9f), 0, new Vector3(1.6f, 0.02f, 1.6f), solid: false);
        }

        void Bathroom()
        {
            Furn("bathtub", new Vector3(0.48f, 0, 2.5f), 90, new Vector3(1.7f, 0.6f, 0.8f));
            Furn("toilet", new Vector3(2.55f, 0, Z1 - 0.4f), 180, new Vector3(0.45f, 0.8f, 0.7f));
            var sink = Furn("bathroomSink", new Vector3(1.35f, 0, Z1 - 0.28f), 180, new Vector3(0.6f, 0.9f, 0.5f));
            Furn("bathroomMirror", new Vector3(1.35f, 1.15f, Z1 - 0.08f), 180, new Vector3(0.55f, 0.75f, 0.06f), solid: false);
            Tissue(new Vector3(2.1f, 0.06f, Z1 - 0.3f));
            Tissue(new Vector3(2.95f - 0.15f, 0.06f, 3.6f));
            Tissue(new Vector3(1.0f, 0.65f, 1.9f));
            Cup(At(1.12f, Z1 - 0.3f), new Color(0.6f, 0.85f, 0.85f));
            Dyn("trashcan", new Vector3(2.8f, 0, 3.0f), 0, new Vector3(0.28f, 0.38f, 0.28f), "휴지통", PropKind.Trash, 0.8f);
        }

        void Entry()
        {
            Furn("rugDoormat", new Vector3(4.5f, 0, Z1 - 0.55f), 0, new Vector3(1.0f, 0.02f, 0.6f), solid: false);
            Dyn("coatRack", new Vector3(5.65f, 0, 4.4f), -90, new Vector3(0.4f, 1.75f, 0.4f), "옷걸이", PropKind.Thing, 2.5f, big: true);
            Haven("cardboard_box_01", new Vector3(3.45f, 0, 4.45f), 10, new Vector3(0.55f, 0.45f, 0.55f), "택배 상자", 2f);
            Haven("cardboard_box_01", new Vector3(3.5f, 0.45f, 4.45f), -12, new Vector3(0.45f, 0.38f, 0.45f), "택배 상자", 1.4f);
            Haven("plastic_crate_01", new Vector3(5.55f, 0, 2.0f), 80, new Vector3(0.5f, 0.32f, 0.38f), "바구니", 1.5f);
            Haven("trashbag", new Vector3(3.4f, 0, 1.6f), 30, new Vector3(0.5f, 0.6f, 0.5f), "쓰레기봉투", 2f);
            Furn("sideTableDrawers", new Vector3(5.65f, 0, 3.0f), -90, new Vector3(0.45f, 0.75f, 0.5f));
            Bowl(At(5.65f, 3.15f));
            Vase(At(5.65f, 2.85f), new Color(0.35f, 0.45f, 0.75f), new Color(0.95f, 0.95f, 0.6f));
            Shoes(new Vector3(4.1f, 0, 4.2f), new Color(0.9f, 0.3f, 0.25f));
            Shoes(new Vector3(4.9f, 0, 4.15f), new Color(0.25f, 0.3f, 0.35f));
        }

        // ================================================================== shape-built props
        Prop Proc(string label, PropKind kind, Vector3 at, float yaw, float mass, Vector3 box, System.Action<Transform> build)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            build(go.transform);
            var c = go.AddComponent<BoxCollider>();
            c.size = box;
            c.center = new Vector3(0, box.y * 0.5f, 0);
            return Register(go, label, kind, mass, false);
        }

        void Cup(Vector3 at, Color c) => Proc("컵", PropKind.Fragile, at, Random.Range(0, 360), 0.25f, new Vector3(0.08f, 0.1f, 0.08f), t =>
        {
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.05f, 0), new Vector3(0.08f, 0.05f, 0.08f), Mats.Flat(c, 0.75f));
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.097f, 0), new Vector3(0.066f, 0.002f, 0.066f), Mats.Flat(new Color(0.35f, 0.2f, 0.1f), 0.9f));
        });

        void Mug(Vector3 at, Color c) => Proc("머그컵", PropKind.Fragile, at, Random.Range(0, 360), 0.35f, new Vector3(0.1f, 0.1f, 0.1f), t =>
        {
            var m = Mats.Flat(c, 0.7f);
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.05f, 0), new Vector3(0.09f, 0.05f, 0.09f), m);
            Mats.Shape(PrimitiveType.Cube, t, new Vector3(0.055f, 0.05f, 0), new Vector3(0.02f, 0.06f, 0.035f), m);
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.097f, 0), new Vector3(0.076f, 0.002f, 0.076f), Mats.Flat(new Color(0.9f, 0.85f, 0.75f), 0.9f));
        });

        void Glass(Vector3 at) => Proc("유리잔", PropKind.Fragile, at, 0, 0.2f, new Vector3(0.07f, 0.12f, 0.07f), t =>
        {
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.06f, 0), new Vector3(0.07f, 0.06f, 0.07f), Mats.Flat(new Color(0.75f, 0.9f, 0.95f), 0.95f));
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.05f, 0), new Vector3(0.06f, 0.045f, 0.06f), Mats.Flat(new Color(0.55f, 0.8f, 0.95f), 0.95f, 0f, 0.3f));
        });

        void Plates(Vector3 at) => Proc("접시", PropKind.Fragile, at, 0, 0.6f, new Vector3(0.22f, 0.05f, 0.22f), t =>
        {
            for (int i = 0; i < 3; i++)
                Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.009f + i * 0.016f, 0), new Vector3(0.22f - i * 0.01f, 0.007f, 0.22f - i * 0.01f),
                    Mats.Flat(i == 1 ? new Color(0.75f, 0.85f, 0.95f) : new Color(0.97f, 0.97f, 0.95f), 0.8f));
        });

        void Vase(Vector3 at, Color body, Color petal) => Proc("꽃병", PropKind.Fragile, at, Random.Range(0, 360), 0.8f, new Vector3(0.12f, 0.42f, 0.12f), t =>
        {
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.11f, 0), new Vector3(0.11f, 0.11f, 0.11f), Mats.Flat(body, 0.8f));
            Mats.Shape(PrimitiveType.Sphere, t, new Vector3(0, 0.07f, 0), new Vector3(0.13f, 0.12f, 0.13f), Mats.Flat(body, 0.8f));
            var stem = Mats.Flat(new Color(0.3f, 0.6f, 0.3f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90 + 20, r = 0.03f;
                var tip = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r, 0.36f + (i % 2) * 0.04f, Mathf.Sin(a * Mathf.Deg2Rad) * r);
                Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(tip.x * 0.5f, 0.28f, tip.z * 0.5f), new Vector3(0.008f, 0.08f, 0.008f), stem, new Vector3(tip.z * 200, 0, -tip.x * 200));
                Mats.Shape(PrimitiveType.Sphere, t, tip, Vector3.one * 0.055f, Mats.Flat(i == 2 ? Color.white : petal, 0.2f));
            }
        });

        void Frame(Vector3 at, float yaw, Color photo) => Proc("액자", PropKind.Fragile, at, yaw, 0.4f, new Vector3(0.18f, 0.22f, 0.04f), t =>
        {
            Mats.Shape(PrimitiveType.Cube, t, new Vector3(0, 0.11f, 0), new Vector3(0.18f, 0.22f, 0.025f), Mats.Flat(new Color(0.4f, 0.28f, 0.2f), 0.5f));
            Mats.Shape(PrimitiveType.Cube, t, new Vector3(0, 0.115f, 0.014f), new Vector3(0.14f, 0.17f, 0.004f), Mats.Flat(photo, 0.9f));
            Mats.Shape(PrimitiveType.Sphere, t, new Vector3(0, 0.13f, 0.017f), new Vector3(0.07f, 0.08f, 0.004f), Mats.Flat(new Color(0.55f, 0.55f, 0.6f)));
        });

        void Bottle(Vector3 at, Color c) => Proc("병", PropKind.Fragile, at, 0, 0.5f, new Vector3(0.07f, 0.3f, 0.07f), t =>
        {
            var m = Mats.Flat(c, 0.9f);
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.1f, 0), new Vector3(0.07f, 0.1f, 0.07f), m);
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.25f, 0), new Vector3(0.025f, 0.05f, 0.025f), m);
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.1f, 0), new Vector3(0.072f, 0.04f, 0.072f), Mats.Flat(new Color(0.95f, 0.92f, 0.8f), 0.3f));
        });

        /// <summary>Glass bowl with water and a goldfish: tip or break it and the floor gets a puddle and a flapping fish.</summary>
        void Fishbowl(Vector3 at) => Proc("어항", PropKind.Water, at, 0, 1.2f, new Vector3(0.22f, 0.2f, 0.22f), t =>
        {
            Mats.Shape(PrimitiveType.Sphere, t, new Vector3(0, 0.1f, 0), new Vector3(0.22f, 0.2f, 0.22f), Mats.Flat(new Color(0.8f, 0.93f, 1f), 0.97f));
            Mats.Shape(PrimitiveType.Sphere, t, new Vector3(0, 0.09f, 0), new Vector3(0.2f, 0.15f, 0.2f), Mats.Flat(new Color(0.35f, 0.7f, 0.95f), 0.95f, 0, 0.35f));
            Mats.Shape(PrimitiveType.Sphere, t, new Vector3(0.02f, 0.1f, 0.03f), new Vector3(0.05f, 0.03f, 0.02f), Mats.Flat(new Color(1f, 0.5f, 0.1f), 0.6f, 0, 0.6f));
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.01f, 0), new Vector3(0.12f, 0.005f, 0.12f), Mats.Flat(new Color(0.85f, 0.8f, 0.65f), 0.2f));
        });

        void Remote(Vector3 at) => Proc("리모컨", PropKind.Thing, at, 30, 0.15f, new Vector3(0.05f, 0.022f, 0.16f), t =>
        {
            Mats.Shape(PrimitiveType.Cube, t, new Vector3(0, 0.011f, 0), new Vector3(0.05f, 0.022f, 0.16f), Mats.Flat(new Color(0.15f, 0.15f, 0.17f), 0.6f));
            Mats.Shape(PrimitiveType.Cube, t, new Vector3(0, 0.023f, 0.05f), new Vector3(0.015f, 0.004f, 0.015f), Mats.Flat(new Color(0.9f, 0.2f, 0.2f), 0.6f));
        });

        void Clock(Vector3 at) => Proc("자명종", PropKind.Fragile, at, 160, 0.4f, new Vector3(0.12f, 0.13f, 0.06f), t =>
        {
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.065f, 0), new Vector3(0.12f, 0.03f, 0.12f), Mats.Flat(new Color(0.95f, 0.45f, 0.4f), 0.7f), new Vector3(90, 0, 0));
            Mats.Shape(PrimitiveType.Cylinder, t, new Vector3(0, 0.065f, 0.031f), new Vector3(0.095f, 0.002f, 0.095f), Mats.Flat(Color.white, 0.5f), new Vector3(90, 0, 0));
        });

        void Yarn(Vector3 at, Color c)
        {
            var go = new GameObject("털실");
            go.transform.SetParent(_root, false);
            go.transform.position = at;
            Mats.Shape(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.12f, Mats.Flat(c, 0.1f));
            Mats.Shape(PrimitiveType.Cylinder, go.transform, new Vector3(0.05f, -0.055f, 0.03f), new Vector3(0.01f, 0.08f, 0.01f), Mats.Flat(c, 0.1f), new Vector3(0, 40, 85));
            go.AddComponent<SphereCollider>().radius = 0.06f;
            Register(go, "털실", PropKind.Thing, 0.1f, false);
        }

        void Tissue(Vector3 at)
        {
            var go = new GameObject("휴지");
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, Random.Range(0, 180), 0));
            var paper = Mats.Shape(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.11f, 0.055f, 0.11f), Mats.Saved("Paper"), new Vector3(0, 0, 90));
            Mats.Shape(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.04f, 0.057f, 0.04f), Mats.Flat(new Color(0.7f, 0.55f, 0.4f)), new Vector3(0, 0, 90));
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 0;
            c.radius = 0.055f;
            c.height = 0.12f;
            var p = Register(go, "휴지", PropKind.Tissue, 0.12f, false);
            p.Paper = paper.transform;
            p.Body.angularDamping = 0.3f;
        }

        void Bowl(Vector3 at)
        {
            Proc("그릇", PropKind.Fragile, at, 0, 0.5f, new Vector3(0.24f, 0.07f, 0.24f), t =>
            {
                Mats.Shape(PrimitiveType.Sphere, t, new Vector3(0, 0.06f, 0), new Vector3(0.24f, 0.12f, 0.24f), Mats.Flat(new Color(0.95f, 0.88f, 0.75f), 0.6f));
            });
            var apple = new[] { new Color(0.85f, 0.15f, 0.15f), new Color(0.6f, 0.8f, 0.2f), new Color(1f, 0.6f, 0.1f) };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("사과");
                go.transform.SetParent(_root, false);
                go.transform.position = at + new Vector3((i - 1) * 0.06f, 0.14f + (i == 1 ? 0.04f : 0), (i == 1 ? 0.03f : -0.02f));
                Mats.Shape(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.075f, Mats.Flat(apple[i], 0.6f));
                Mats.Shape(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.04f, 0), new Vector3(0.006f, 0.012f, 0.006f), Mats.Flat(new Color(0.35f, 0.22f, 0.1f)));
                go.AddComponent<SphereCollider>().radius = 0.037f;
                Register(go, i == 2 ? "귤" : "사과", PropKind.Thing, 0.15f, false);
            }
        }

        void Shoes(Vector3 at, Color c)
        {
            for (int i = 0; i < 2; i++)
                Proc("신발", PropKind.Thing, at + new Vector3(i * 0.16f, 0, 0), Random.Range(-15f, 15f), 0.4f, new Vector3(0.1f, 0.09f, 0.26f), t =>
                {
                    Mats.Shape(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.26f), Mats.Flat(Color.white, 0.3f));
                    Mats.Shape(PrimitiveType.Cube, t, new Vector3(0, 0.06f, -0.03f), new Vector3(0.09f, 0.05f, 0.17f), Mats.Flat(c, 0.3f));
                });
        }

        /// <summary>The robot vacuum's charging dock in the kitchen corner.</summary>
        void DockBase()
        {
            var root = new GameObject("충전대").transform;
            root.SetParent(_root, false);
            root.SetPositionAndRotation(Dock + new Vector3(0.28f, 0, 0), Quaternion.Euler(0, DockYaw, 0));
            Mats.Shape(PrimitiveType.Cube, root, new Vector3(0, 0.06f, 0), new Vector3(0.24f, 0.12f, 0.06f), Mats.Flat(new Color(0.93f, 0.93f, 0.95f), 0.7f));
            Mats.Shape(PrimitiveType.Cube, root, new Vector3(0, 0.01f, 0.08f), new Vector3(0.24f, 0.02f, 0.12f), Mats.Flat(new Color(0.2f, 0.2f, 0.22f), 0.5f));
            Mats.Shape(PrimitiveType.Sphere, root, new Vector3(0, 0.1f, 0.032f), Vector3.one * 0.015f, Mats.Flat(new Color(0.3f, 1f, 0.5f), 0.8f, 0, 3f));
        }

        /// <summary>The cat's own cushion: hide here before the owner comes home.</summary>
        void Bed(Vector3 at)
        {
            var root = new GameObject("고양이 방석").transform;
            root.SetParent(_root, false);
            root.position = at;
            var soft = Mats.Flat(new Color(0.95f, 0.72f, 0.78f), 0.1f);
            Mats.Shape(PrimitiveType.Cylinder, root, new Vector3(0, 0.025f, 0), new Vector3(0.62f, 0.025f, 0.62f), Mats.Flat(new Color(0.98f, 0.92f, 0.85f), 0.1f));
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2 / 14;
                Mats.Shape(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(a) * 0.3f, 0.07f, Mathf.Sin(a) * 0.3f), new Vector3(0.18f, 0.13f, 0.18f), soft);
            }
            var c = root.gameObject.AddComponent<BoxCollider>();
            c.size = new Vector3(0.6f, 0.03f, 0.6f);
            c.center = new Vector3(0, 0.015f, 0);
        }

        // ================================================================== model helpers
        /// <summary>Static furniture with mesh colliders (seats, shelves and counters are walkable).</summary>
        GameObject Furn(string id, Vector3 at, float yaw, Vector3 size, bool solid = true, bool keepAspect = true)
        {
            var go = Fit("Art/Furniture/" + id, size, keepAspect, id);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            if (solid)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            else foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            Physics.SyncTransforms();
            return go;
        }

        Prop Dyn(string id, Vector3 at, float yaw, Vector3 size, string label, PropKind kind, float mass, bool big = false)
        {
            var go = Fit("Art/Furniture/" + id, size, true, label);
            return Loose(go, at, yaw, label, kind, mass, big);
        }

        void Haven(string id, Vector3 at, float yaw, Vector3 size, string label, float mass)
        {
            var go = Fit($"Art/Haven/Models/{id}/{id}", size, true, label);
            var lit = Resources.Load<Material>($"Art/Haven/Models/{id}/Lit");
            if (lit) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(lit, r.sharedMaterials.Length).ToArray();
            Loose(go, at, yaw, label, PropKind.Thing, mass, mass > 1.8f);
        }

        Prop Loose(GameObject go, Vector3 at, float yaw, string label, PropKind kind, float mass, bool big)
        {
            var b = Bounds(go);
            var c = go.AddComponent<BoxCollider>();
            c.size = b.size;
            c.center = b.center;
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.003f, Quaternion.Euler(0, yaw, 0));
            Physics.SyncTransforms();
            return Register(go, label, kind, mass, big);
        }

        Prop Register(GameObject go, string label, PropKind kind, float mass, bool big)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = mass < 1f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            rb.angularDamping = 0.1f;
            var p = go.AddComponent<Prop>();
            p.Label = label;
            p.Kind = kind;
            p.Big = big;
            p.Body = rb;
            Props.Add(p);
            return p;
        }

        /// <summary>Instantiates a model under a fresh root, scaled into <paramref name="size"/> with its feet on the root's origin.</summary>
        GameObject Fit(string res, Vector3 size, bool keepAspect, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_root, false);
            var prefab = Resources.Load<GameObject>(res);
            if (!prefab) { Debug.LogWarning("[house] missing " + res); return root; }
            var inst = Object.Instantiate(prefab, root.transform, false);
            var b = Bounds(inst, root.transform);
            var s = Vector3.one;
            if (keepAspect)
            {
                float k = float.MaxValue;
                for (int i = 0; i < 3; i++) if (size[i] > 0.0001f && b.size[i] > 0.0001f) k = Mathf.Min(k, size[i] / b.size[i]);
                s = Vector3.one * k;
            }
            else for (int i = 0; i < 3; i++) s[i] = b.size[i] > 0.0001f ? size[i] / b.size[i] : 1f;
            inst.transform.localScale = Vector3.Scale(inst.transform.localScale, s);
            b = Bounds(inst, root.transform);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            return root;
        }

        /// <summary>Renderer bounds in <paramref name="space"/> (defaults to the object's own root).</summary>
        static Bounds Bounds(GameObject g, Transform space = null)
        {
            space ??= g.transform;
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            bool first = true;
            var b = new Bounds();
            foreach (var r in rs)
            {
                var lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Top of whatever is under (x, z), searching down from <paramref name="from"/>.</summary>
        static Vector3 At(float x, float z, float from = 2.4f)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(x, from, z), Vector3.down, out var hit, from + 1f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.002f
                : new Vector3(x, 0, z);
        }

        /// <summary>Heights of the boards of an open bookcase, found by probing downwards.</summary>
        static float[] ShelfHeights(GameObject shelf, float x, float z)
        {
            var list = new List<float>();
            float y = Bounds(shelf, shelf.transform.parent).max.y - 0.05f;
            while (y > 0.2f)
            {
                if (!Physics.Raycast(new Vector3(x, y, z), Vector3.down, out var hit, y, ~(1 << 2))) break;
                if (hit.point.y < 0.15f) break;
                list.Add(hit.point.y + 0.002f);
                y = hit.point.y - 0.06f;
            }
            return list.ToArray();
        }

        /// <summary>A soft warm bulb that rides along when the lamp is knocked over.</summary>
        void Bulb(Transform lamp, float y, float intensity, float range)
        {
            var l = new GameObject("bulb").AddComponent<Light>();
            l.transform.SetParent(lamp, false);
            l.transform.localPosition = new Vector3(0, y, 0);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.8f, 0.55f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            LampSpots.Add(lamp.position);
        }

        /// <summary>Glowing picture on the TV so it reads as switched on.</summary>
        static void Screen(Transform tv, Vector3 size)
        {
            var q = Mats.Shape(PrimitiveType.Quad, tv, new Vector3(0, size.y * 0.55f, size.z * 0.5f + 0.012f), new Vector3(size.x * 0.86f, size.y * 0.62f, 1),
                Mats.Flat(new Color(0.35f, 0.75f, 1f), 0.9f, 0, 1.6f), new Vector3(0, 180, 0));
            Mats.NoShadows(q);
        }

        // ================================================================== shell helpers
        static Vector4 Win(float c, float w, float sill = 0.8f) => new(c, w, sill, 2.1f);
        static Vector4 Gap(float c, float w) => new(c, w, 0f, 2.1f);

        void Floor(Rect r, string mat) =>
            Block("바닥", new Vector3(r.center.x, -0.05f, r.center.y), new Vector3(r.width, 0.1f, r.height), Mats.Saved(mat));

        void Wall(bool alongX, float at, float a0, float a1, params Vector4[] gaps)
        {
            float cur = a0;
            foreach (var g in gaps.OrderBy(g => g.x))
            {
                float g0 = g.x - g.y / 2, g1 = g.x + g.y / 2;
                Seg(alongX, at, cur, g0, 0, H);
                if (g.z > 0) { Seg(alongX, at, g0, g1, 0, g.z); Window(alongX, at, g0, g1, g.z, g.w); }
                Seg(alongX, at, g0, g1, g.w, H, plain: true);
                cur = g1;
            }
            Seg(alongX, at, cur, a1, 0, H);
        }

        void Seg(bool alongX, float at, float a, float b, float y0, float y1, bool plain = false)
        {
            if (b - a < 0.01f || y1 - y0 < 0.01f) return;
            float mid = (a + b) / 2, len = b - a;
            Block("벽", alongX ? new Vector3(mid, (y0 + y1) / 2, at) : new Vector3(at, (y0 + y1) / 2, mid),
                alongX ? new Vector3(len, y1 - y0, Wt) : new Vector3(Wt, y1 - y0, len), _wall);
            if (y0 == 0 && !plain)   // skirting board
                Block("걸레받이", alongX ? new Vector3(mid, 0.04f, at) : new Vector3(at, 0.04f, mid),
                    alongX ? new Vector3(len, 0.08f, Wt + 0.03f) : new Vector3(Wt + 0.03f, 0.08f, len), _trim, collide: false);
        }

        /// <summary>Glowing pane, white frame and a deep sill the cat can sit on.</summary>
        void Window(bool alongX, float at, float a, float b, float sill, float top)
        {
            float mid = (a + b) / 2, len = b - a, h = top - sill;
            Vector3 P(float along, float y, float off = 0) => alongX ? new Vector3(along, y, at + off) : new Vector3(at + off, y, along);
            Vector3 S(float along, float y, float depth) => alongX ? new Vector3(along, y, depth) : new Vector3(depth, y, along);
            float inward = alongX ? (at < 0 ? 1 : -1) : (at < 0 ? 1 : -1);
            var pane = Block("창", P(mid, sill + h / 2), S(len, h, 0.03f), Mats.Saved("Sky"));
            pane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Block("창턱", P(mid, sill - 0.02f, inward * 0.08f), S(len + 0.16f, 0.05f, 0.32f), _frame);
            Block("창틀", P(mid, top + 0.03f, 0), S(len + 0.1f, 0.06f, Wt + 0.04f), _frame, collide: false);
            Block("창틀", P(a - 0.03f, sill + h / 2, 0), S(0.06f, h, Wt + 0.04f), _frame, collide: false);
            Block("창틀", P(b + 0.03f, sill + h / 2, 0), S(0.06f, h, Wt + 0.04f), _frame, collide: false);
            Block("창살", P(mid, sill + h / 2, 0), S(0.04f, h, 0.05f), _frame, collide: false);
        }

        GameObject Block(string name, Vector3 c, Vector3 s, Material m, bool collide = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(_root, false);
            g.transform.localPosition = c;
            g.transform.localScale = s;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collide) Object.DestroyImmediate(g.GetComponent<Collider>());
            else g.AddComponent<CamBlock>();
            return g;
        }
    }
}
