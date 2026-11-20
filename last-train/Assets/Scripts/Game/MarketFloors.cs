using UnityEngine;

namespace LastTrain
{
    public partial class City
    {
        internal static readonly Vector2 MarketStairBottom = new(22.06f, 24.75f);
        internal static readonly Vector2 MarketStairTop = new(21.8f, -88.15f);
        internal static readonly Vector2 MarketUpperArrival = new(21.8f, -87.05f);
        internal static readonly Vector2 MarketUpperOccupant = new(23.3f, -84.2f);
        internal static readonly Rect MarketUpperRoom = new(16.8f, -89.1f, 8.6f, 6.6f);
        internal Rect MarketGroundRoom { get; private set; }

        Transform BuildMarketGroundFloor(Vector2 at, Vector2 size, float eaves, float top, Material wall, Material trim)
        {
            var footprint = new Rect(at - size * 0.5f, size);
            MarketGroundRoom = footprint;
            var usableRoom = Rect.MinMaxRect(footprint.xMin, footprint.yMin + 0.20f, footprint.xMax, footprint.yMax);
            InteriorEntrances.Add(("시장 관리동", usableRoom, new Vector2(at.x, footprint.yMin + 0.10f)));
            var upper = new GameObject("Market office upper facade and roof").transform;
            upper.SetParent(transform, false);
            var bounds = new Bounds(Fx.V3(at, top * 0.5f), new Vector3(size.x, top, size.y));
            _houses.Add(new House { bounds = bounds, upper = upper.gameObject });
            Part(upper, at, (2.25f + eaves) * 0.5f, new Vector3(size.x, eaves - 2.25f, size.y), wall);
            Window(at, size, -1.45f, 3.35f, 1.52f, 0.90f, trim, upper);
            Window(at, size, 1.45f, 3.35f, 1.52f, 0.90f, trim, upper);
            Part(transform, at, 0.071f, new Vector3(size.x, 0.12f, size.y), Resources.Load<Material>("Materials/WornFloor"));

            const float width = 0.20f;
            SiteBarrier(new Vector2(footprint.center.x, footprint.yMax - width * 0.5f), new Vector2(size.x, width), 2.25f, wall);
            SiteBarrier(new Vector2(footprint.xMin + width * 0.5f, footprint.center.y), new Vector2(width, size.y), 2.25f, wall);
            SiteBarrier(new Vector2(footprint.xMax - width * 0.5f, footprint.center.y), new Vector2(width, size.y), 0.87f, wall);
            float jamb = (size.x - 1.62f) * 0.5f;
            foreach (int side in new[] { -1, 1 })
                SiteBarrier(new Vector2(at.x + side * (0.81f + jamb * 0.5f), footprint.yMin + width * 0.5f),
                    new Vector2(jamb, width), 0.84f, wall);
            Part(upper, new Vector2(at.x, footprint.yMin + 0.15f), 2.25f, new Vector3(1.66f, 0.18f, 0.32f), trim);

            AddInteriorLoot(footprint, new Vector2(19.33f, 25.63f), "1층 접수함", "store", null,
                new Vector3(1.35f, 0.73f, 0.55f), new Color(0.62f, 0.46f, 0.26f));
            BuildStairSteps(MarketStairBottom + Vector2.up * 0.44f, trim, ascending: true);
            SiteLamp(new Vector2(19.4f, footprint.yMax - 0.20f), 2.16f, new Color(1f, 0.82f, 0.61f));
            Shops.Add(new Shop { name = "시장 관리동 1층", pos = new Vector2(at.x, footprint.yMin - 0.48f), color = new Color(0.85f, 0.75f, 0.57f) });
            return upper;
        }

        void BuildMarketUpperFloor()
        {
            var r = MarketUpperRoom;
            var wall = DistrictMaterial("office upstairs plaster", "Plaster", new Color(0.61f, 0.65f, 0.59f));
            var trim = DistrictMaterial("office upstairs timber", "Planks", new Color(0.37f, 0.28f, 0.20f));
            var surround = DistrictMaterial("office isolation backdrop", "Plain", new Color(0.085f, 0.095f, 0.095f));
            Part(transform, r.center, -0.18f, new Vector3(33f, 0.20f, 30f), surround);
            Part(transform, r.center, 0.018f, new Vector3(r.width, 0.04f, r.height), Resources.Load<Material>("Materials/Planks"));
            SiteBarrier(new Vector2(r.center.x, r.yMin), new Vector2(r.width, 0.24f), 0.70f, wall);
            SiteBarrier(new Vector2(r.center.x, r.yMax), new Vector2(r.width, 0.24f), 2.50f, wall);
            SiteBarrier(new Vector2(r.xMin, r.center.y), new Vector2(0.24f, r.height), 2.35f, wall);
            SiteBarrier(new Vector2(r.xMax, r.center.y), new Vector2(0.24f, r.height), 0.76f, wall);

            // A hallway separates the archive from the staff room. Both side passages remain open.
            SiteBarrier(new Vector2(20.55f, -84.2f), new Vector2(0.22f, 3.20f), 1.06f, wall);
            AddInteriorLoot(r, new Vector2(18.52f, -83.47f), "2층 기록 보관함", "hardware", null,
                new Vector3(1.48f, 0.94f, 0.57f), new Color(0.65f, 0.57f, 0.39f));
            AddInteriorLoot(r, new Vector2(24.12f, -83.54f), "2층 휴게실 구급함", "pharmacy", "medicine_cabinet",
                new Vector3(0.92f, 1.19f, 0.55f), new Color(0.43f, 0.68f, 0.57f));
            SiteProp("metal_office_desk", new Vector2(17.97f, -87.4f), 1.45f, 0.97f, 0);
            BuildStairSteps(MarketStairTop + Vector2.down * 0.13f, trim, ascending: false);
            SiteLamp(new Vector2(19.2f, r.yMax - 0.20f), 2.30f, new Color(1f, 0.84f, 0.66f));
            SiteLamp(new Vector2(24.35f, r.yMax - 0.20f), 2.18f, new Color(0.61f, 0.82f, 1f));
        }

        void BuildStairSteps(Vector2 center, Material material, bool ascending)
        {
            for (int i = 0; i < 5; i++)
            {
                float height = (ascending ? i + 1 : 5 - i) * 0.145f;
                Part(transform, center + Vector2.up * ((i - 2) * 0.23f), height * 0.5f,
                    new Vector3(1.15f, height, 0.24f), material);
            }
        }
    }

    public partial class Game
    {
        bool _marketUpperVisited, _marketSurvivorHelped;
        GameObject _marketSurvivor;

        bool OnMarketUpper => City.MarketUpperRoom.Contains(Me.Pos);

        bool InteractInteriorEntrance(bool pressed)
        {
            if (InPractice || Phase == "evac" || OnMarketUpper) return false;
            foreach (var entrance in City.InteriorEntrances)
            {
                if ((entrance.door - Me.Pos).sqrMagnitude > 1.35f * 1.35f ||
                    !Clear(Me.Pos, entrance.door)) continue;
                bool inside = entrance.room.Contains(Me.Pos);
                Vector2 arrival = entrance.door + Vector2.up * (inside ? -1.17f : 1.27f);
                if (InWall(arrival, 0.4f, City.Walls)) continue;
                _prompt = inside ? $"E · {entrance.name}에서 나가기" : $"E · {entrance.name} 들어가기";
                if (pressed)
                {
                    Me.Pos = arrival;
                    _moving = false;
                    FramePlayer(0f, true);
                }
                return true;
            }
            return false;
        }

        bool InteractMarketFloors(bool pressed)
        {
            if (InPractice || Phase == "evac") return false;
            bool above = OnMarketUpper;
            var stair = above ? City.MarketStairTop : City.MarketStairBottom;
            if ((above || City.MarketGroundRoom.Contains(Me.Pos)) &&
                Vector2.Distance(stair, Me.Pos) < 0.76f)
            {
                _prompt = above ? "E · 1층으로 내려가기" : "E · 2층으로 올라가기";
                if (pressed)
                {
                    Me.Pos = above ? City.MarketStairBottom + Vector2.down * 0.68f : City.MarketUpperArrival;
                    _moving = false;
                    FramePlayer(0f, true);
                    Say(above ? "시장 관리동 · 1층 접수실" : "시장 관리동 · 2층 기록실");
                    if (!above) PopulateMarketUpper(_encounterSeed);
                }
                return true;
            }
            if (!above || !_marketSurvivor || _marketSurvivorHelped ||
                Vector2.Distance(Me.Pos, City.MarketUpperOccupant) >= 1.4f || !Clear(Me.Pos, City.MarketUpperOccupant))
                return false;
            bool hasBandage = Me.Bag["bandage"] > 0;
            _prompt = hasBandage ? "E · 다친 관리인에게 붕대 1개 건네기" : "다친 관리인 · 붕대가 필요하다";
            if (pressed && hasBandage)
            {
                Me.Bag["bandage"]--;
                _marketSurvivorHelped = true;
                _peopleHelped++;
                Me.Take("scrap", 2, out int taken);
                if (taken < 2) LeaveSupplies(City.MarketUpperOccupant, ("scrap", 2 - taken));
                Say("관리인: 그쪽 접수실에서 가져온 고철이에요. 받아요.");
            }
            return true;
        }

        void VerifyMarketFloors()
        {
            var office = City.InteriorEntrances.Find(entry => entry.name == "시장 관리동");
            foreach (var door in City.InteriorEntrances)
            {
                Me.Pos = door.door + Vector2.down * 1.0f;
                Interact(false, false, 0f);
                if (!_prompt.Contains("들어가기")) throw new System.InvalidOperationException("Missing entrance prompt for " + door.name);
                Interact(false, true, 0f);
                if (!door.room.Contains(Me.Pos)) throw new System.InvalidOperationException("Entrance button failed for " + door.name);
                Interact(false, true, 0f);
                if (door.room.Contains(Me.Pos)) throw new System.InvalidOperationException("Exit button failed for " + door.name);
            }
            Me.Pos = office.door + Vector2.down * 1.0f;
            Interact(false, true, 0f);
            var groundCache = City.Containers.Find(c => c.kind == "1층 접수함");
            if (!City.MarketGroundRoom.Contains(Me.Pos) || !ContainerAccessible(groundCache))
                throw new System.InvalidOperationException("The ground-floor doorway is blocked");
            WalkTo(groundCache.pos);
            Interact(true, true, 0f);
            if (_searching != groundCache) throw new System.InvalidOperationException("Cannot search the ground-floor reception");
            FinishSearch(groundCache);
            CloseLoot();
            WalkTo(new Vector2(22.06f, 24.3f));
            int previousZombies = Horde.Count;
            if (!InteractMarketFloors(true) || !OnMarketUpper || ContainerAccessible(groundCache))
                throw new System.InvalidOperationException("The stairs did not isolate the upper floor");

            int roll = (_encounterSeed % 3 + 3) % 3;
            if (Horde.Count != previousZombies + (roll == 0 ? 1 : 0) || (_marketSurvivor != null) != (roll == 1))
                throw new System.InvalidOperationException("The upper-floor encounter did not match the seed");
            var upperCache = City.Containers.Find(c => c.kind == "2층 기록 보관함");
            WalkTo(new Vector2(19.35f, -86.15f));
            WalkTo(upperCache.pos);
            Interact(true, true, 0f);
            if (_searching != upperCache) throw new System.InvalidOperationException("Cannot search the upper-floor archive");
            FinishSearch(upperCache);
            CloseLoot();
            WalkTo(new Vector2(19.35f, -85.95f));
            WalkTo(new Vector2(22.4f, -87.0f));
            if (roll == 1)
            {
                WalkTo(City.MarketUpperOccupant + Vector2.down * 0.95f);
                int scrap = Me.Bag["scrap"];
                Me.Take("bandage", 1);
                InteractMarketFloors(true);
                if (!_marketSurvivorHelped || Me.Bag["bandage"] != 0 || Me.Bag["scrap"] != scrap + 2)
                    throw new System.InvalidOperationException("Upper-floor survivor exchange failed");
            }
            WalkTo(City.MarketStairTop);
            if (!InteractMarketFloors(true) || OnMarketUpper) throw new System.InvalidOperationException("Cannot return downstairs");
            WalkTo(City.MarketStairBottom);
            if (!InteractMarketFloors(true) || !OnMarketUpper ||
                Horde.Count != previousZombies + (roll == 0 ? 1 : 0))
                throw new System.InvalidOperationException("Upper-floor occupants respawned");
            Debug.Log($"[market-floors] entry, stairs, search, return and seed {roll} occupant verified");

            void WalkTo(Vector2 target)
            {
                for (int i = 0; i < 170 && Vector2.Distance(Me.Pos, target) > 0.08f; i++)
                    Me.Pos = Horde.Slide(Me.Pos, Vector2.MoveTowards(Me.Pos, target, 0.08f) - Me.Pos, 0.4f, City.Walls);
                if (Vector2.Distance(Me.Pos, target) > 0.10f)
                    throw new System.InvalidOperationException($"Floor blocked near {target} (stopped at {Me.Pos})");
            }
        }

        void PopulateMarketUpper(int seed)
        {
            if (_marketUpperVisited) return;
            _marketUpperVisited = true;
            int roll = (seed % 3 + 3) % 3;
            if (roll == 0)
                Horde.Spawn(City.MarketUpperOccupant, 0);
            else if (roll == 1)
                _marketSurvivor = _actors.SpawnNpc("Worker_Male", City.MarketUpperOccupant, 180);
        }
    }
}
