using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    /// <summary>
    /// The zombie horde as flat lists — no colliders, no physics — so hundreds stay cheap.
    /// Each zombie is either unaware (wandering) or aware and heading for a goal the game sets
    /// (see Game.Senses: sight cones and noise).
    /// Positions are on the ground plane (x, z) as Vector2. A uniform grid rebuilt each step
    /// answers "who is near here" for separation and weapon hits.
    /// ponytail: walls are a linear scan of rects; add a wall grid if maps get big.
    /// </summary>
    public class Horde
    {
        const float Cell = 1.5f;

        public readonly List<Vector2> Pos = new();
        public readonly List<float> Hp = new();
        public readonly List<int> Kind = new();
        public readonly List<float> Slow = new();
        public readonly List<float> Flash = new();
        /// <summary>Knockback velocity, spent over a few frames so a blow shoves instead of teleporting.</summary>
        public readonly List<Vector2> Knock = new();
        public readonly List<Vector2> Heading = new();
        /// <summary>Stable id per zombie so views can follow it through swap-removes.</summary>
        public readonly List<int> Id = new();
        /// <summary>Seconds of alertness left; 0 = unaware (wandering, can be stealth-killed).</summary>
        public readonly List<float> Aware = new();
        /// <summary>Where an aware zombie is heading: a survivor, a noise, the shelter door.</summary>
        public readonly List<Vector2> Goal = new();

        internal const int Screamer = 3, Armored = 4;
        readonly Tuning.ZombieCfg[] _types;
        readonly List<Rect> _walls;
        readonly Dictionary<Vector2Int, List<int>> _grid = new();
        readonly Stack<List<int>> _spare = new();
        int _nextId;

        public Horde(Tuning.ZombieCfg[] types, List<Rect> walls)
        {
            _types = types;
            _walls = walls ?? new List<Rect>();
        }

        public int Count => Pos.Count;
        public float Radius(int i) => _types[Kind[i]].radius;
        public Tuning.ZombieCfg Type(int i) => _types[Kind[i]];

        public int Spawn(Vector2 p, int kind, float hpMult = 1f)
        {
            Pos.Add(p);
            Hp.Add(_types[kind].hp * hpMult);
            Kind.Add(kind);
            Slow.Add(0);
            Flash.Add(0);
            Knock.Add(Vector2.zero);
            Heading.Add(Random.insideUnitCircle.normalized);
            Id.Add(_nextId);
            Aware.Add(0);
            Goal.Add(p);
            return _nextId++;
        }

        public bool IsAware(int i) => Aware[i] > 0;

        /// <summary>Makes zombie i chase `goal` for `seconds` (refreshes if already chasing).</summary>
        public void Alert(int i, Vector2 goal, float seconds)
        {
            Aware[i] = Mathf.Max(Aware[i], seconds);
            Goal[i] = goal;
        }

        /// <summary>
        /// Aware zombies run at their goal; unaware ones shamble about, turning now and then.
        /// Then overlaps are separated, nobody stands inside a survivor (`bodies`), walls push out.
        /// </summary>
        public void Step(float dt, IReadOnlyList<Vector2> bodies, int stationaryId = -1, ISet<int> winding = null)
        {
            for (int i = 0; i < Pos.Count; i++)
            {
                var p = Pos[i];
                var t = _types[Kind[i]];
                float slow = Slow[i] > 0 ? 0.4f : 1f;
                if (Aware[i] > 0)
                {
                    Aware[i] -= dt;
                    var to = Goal[i] - p;
                    if (to.sqrMagnitude > 0.04f && !(winding?.Contains(Id[i]) ?? false))
                    {
                        Heading[i] = PursuitHeading(p, Goal[i], t.radius, Heading[i]);
                        p += Heading[i] * t.chaseSpeed * slow * dt;
                    }
                }
                else if (Id[i] != stationaryId)
                {
                    if (Random.value < dt * 0.3f) Heading[i] = (Heading[i] + Random.insideUnitCircle * 1.2f).normalized;
                    float stroll = Mathf.PerlinNoise(Id[i] * 0.37f, Time.time * 0.25f) > 0.45f ? 0.45f : 0f;
                    p += Heading[i] * t.speed * stroll * slow * dt;
                }
                p += Knock[i] * dt;
                Knock[i] *= Mathf.Exp(-12f * dt);
                Pos[i] = p;
                Slow[i] = Mathf.Max(Slow[i] - dt, 0);
                Flash[i] = Mathf.Max(Flash[i] - dt, 0);
            }
            RebuildGrid();
            Separate();
            for (int i = 0; i < Pos.Count; i++)
            {
                var p = Pos[i];
                foreach (var b in bodies)  // zombies crowd around a survivor but never on top of them
                {
                    var d = p - b;
                    float min = Radius(i) + 0.35f;
                    if (d.sqrMagnitude < min * min) p = b + (d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right) * min;
                }
                var before = p;
                p = PushOut(p, Radius(i), _walls);
                if (p != before && Aware[i] <= 0) Heading[i] = (p - before).normalized;  // wanderers turn away from walls
                Pos[i] = p;
            }
            RebuildGrid();
        }

        Vector2 PursuitHeading(Vector2 position, Vector2 goal, float radius, Vector2 previous)
        {
            Vector2 direct = (goal - position).normalized;
            const float lookAhead = 0.34f;
            Vector2 probe = position + direct * lookAhead;
            foreach (var wall in _walls)
            {
                var expanded = Rect.MinMaxRect(wall.xMin - radius, wall.yMin - radius,
                    wall.xMax + radius, wall.yMax + radius);
                if (!expanded.Contains(probe)) continue;

                // Follow the contacted edge to its cheaper corner. Previous motion breaks ties.
                float nearestX = Mathf.Abs(position.x - Mathf.Clamp(position.x, expanded.xMin, expanded.xMax));
                float nearestY = Mathf.Abs(position.y - Mathf.Clamp(position.y, expanded.yMin, expanded.yMax));
                if (nearestX <= 0.001f && nearestY <= 0.001f)
                {
                    nearestX = -Mathf.Min(position.x - expanded.xMin, expanded.xMax - position.x);
                    nearestY = -Mathf.Min(position.y - expanded.yMin, expanded.yMax - position.y);
                }
                bool vertical = nearestX > nearestY;
                float edge = vertical
                    ? (position.x < expanded.center.x ? expanded.xMin : expanded.xMax)
                    : (position.y < expanded.center.y ? expanded.yMin : expanded.yMax);
                const float offset = lookAhead + 0.04f;
                float low = (vertical ? expanded.yMin : expanded.xMin) - offset;
                float high = (vertical ? expanded.yMax : expanded.xMax) + offset;
                float outside = edge + (edge < (vertical ? expanded.center.x : expanded.center.y) ? -offset : offset);
                var a = vertical ? new Vector2(outside, low) : new Vector2(low, outside);
                var b = vertical ? new Vector2(outside, high) : new Vector2(high, outside);
                float costA = Vector2.Distance(position, a) + Vector2.Distance(a, goal);
                float costB = Vector2.Distance(position, b) + Vector2.Distance(b, goal);
                float bias = Vector2.Dot(previous, (b - a).normalized) * 0.25f;
                return ((costA + bias < costB ? a : b) - position).normalized;
            }
            return direct;
        }

        /// <summary>Indices of zombies whose body touches the circle (center, r).</summary>
        public List<int> Near(Vector2 center, float r, List<int> into = null)
        {
            into ??= new List<int>();
            into.Clear();
            int reach = Mathf.CeilToInt((r + 1f) / Cell);
            var c = CellOf(center);
            for (int dx = -reach; dx <= reach; dx++)
            for (int dz = -reach; dz <= reach; dz++)
            {
                if (!_grid.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out var bucket)) continue;
                foreach (int i in bucket)
                {
                    float rr = r + Radius(i);
                    if ((Pos[i] - center).sqrMagnitude <= rr * rr) into.Add(i);
                }
            }
            return into;
        }

        readonly List<int> _scratch = new();

        /// <summary>Nearest zombie index within maxRange, or -1.</summary>
        public int Nearest(Vector2 center, float maxRange)
        {
            int best = -1;
            float bd = maxRange * maxRange;
            foreach (int i in Near(center, maxRange, _scratch))
            {
                float d = (Pos[i] - center).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public void Damage(int i, float amount, float slowSeconds = 0, Vector2 push = default)
        {
            Hp[i] -= amount;
            Flash[i] = 0.12f;
            Slow[i] = Mathf.Max(Slow[i], slowSeconds);
            Pos[i] += push * 0.4f;
            Knock[i] += push * 0.6f * 12f;
        }

        public struct Death { public Vector2 pos; public int kind, id; public Vector2 heading; }

        /// <summary>Removes the dead and returns where they fell (for gems and corpses).</summary>
        public List<Death> Reap()
        {
            var dead = new List<Death>();
            for (int i = Pos.Count - 1; i >= 0; i--)
            {
                if (Hp[i] > 0) continue;
                dead.Add(new Death { pos = Pos[i], kind = Kind[i], id = Id[i], heading = Heading[i] });
                RemoveAt(i);
            }
            if (dead.Count > 0) RebuildGrid();
            return dead;
        }

        public void Despawn(int id)
        {
            int i = Id.IndexOf(id);
            if (i < 0) return;
            RemoveAt(i);
            RebuildGrid();
        }

        /// <summary>Damage per second a body at p with radius r takes from every aware zombie touching it.</summary>
        public float ContactDps(Vector2 p, float r)
        {
            float total = 0;
            foreach (int i in Near(p, r, _scratch)) if (Aware[i] > 0) total += _types[Kind[i]].dps;
            return total;
        }

        // Clip each axis to the first wall face. Movement cannot eject the character
        // through a wall or add any displacement beyond the input step.
        public static Vector2 Slide(Vector2 position, Vector2 step, float radius, List<Rect> walls)
        {
            float x = SlideAxis(position.x, position.y, step.x, radius, walls, true);
            float y = SlideAxis(position.y, x, step.y, radius, walls, false);
            return new Vector2(x, y);
        }

        static float SlideAxis(float start, float cross, float delta, float radius, List<Rect> walls, bool horizontal)
        {
            if (delta == 0f) return start;
            float end = start + delta;
            foreach (var wall in walls)
            {
                float crossMin = (horizontal ? wall.yMin : wall.xMin) - radius;
                float crossMax = (horizontal ? wall.yMax : wall.xMax) + radius;
                if (cross <= crossMin || cross >= crossMax) continue;
                float near = (horizontal ? wall.xMin : wall.yMin) - radius;
                float far = (horizontal ? wall.xMax : wall.yMax) + radius;
                if (delta > 0f && start <= near) end = Mathf.Min(end, near);
                else if (delta < 0f && start >= far) end = Mathf.Max(end, far);
                else if (start > near && start < far)
                {
                    // A spawned or newly obstructed body may move out toward the near side.
                    if ((delta > 0f) == (start - near < far - start)) return start;
                }
            }
            return end;
        }

        /// <summary>Moves a circle out of any wall rect it overlaps (nearest edge wins).</summary>
        public static Vector2 PushOut(Vector2 p, float r, List<Rect> rects)
        {
            foreach (var w in rects)
            {
                var g = new Rect(w.xMin - r, w.yMin - r, w.width + 2 * r, w.height + 2 * r);
                if (!g.Contains(p)) continue;
                float left = p.x - g.xMin, right = g.xMax - p.x, top = p.y - g.yMin, bottom = g.yMax - p.y;
                float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
                if (m == left) p.x = g.xMin;
                else if (m == right) p.x = g.xMax;
                else if (m == top) p.y = g.yMin;
                else p.y = g.yMax;
            }
            return p;
        }

        void RemoveAt(int i)
        {
            int last = Pos.Count - 1;
            Pos[i] = Pos[last]; Hp[i] = Hp[last]; Kind[i] = Kind[last]; Slow[i] = Slow[last]; Knock[i] = Knock[last];
            Flash[i] = Flash[last]; Heading[i] = Heading[last]; Id[i] = Id[last]; Aware[i] = Aware[last]; Goal[i] = Goal[last];
            Pos.RemoveAt(last); Hp.RemoveAt(last); Kind.RemoveAt(last); Slow.RemoveAt(last); Knock.RemoveAt(last);
            Flash.RemoveAt(last); Heading.RemoveAt(last); Id.RemoveAt(last); Aware.RemoveAt(last); Goal.RemoveAt(last);
        }

        static Vector2Int CellOf(Vector2 p) => new(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));

        void RebuildGrid()
        {
            foreach (var l in _grid.Values) { l.Clear(); _spare.Push(l); }
            _grid.Clear();
            for (int i = 0; i < Pos.Count; i++)
            {
                var c = CellOf(Pos[i]);
                if (!_grid.TryGetValue(c, out var l)) _grid[c] = l = _spare.Count > 0 ? _spare.Pop() : new List<int>();
                l.Add(i);
            }
        }

        static readonly Vector2Int[] HalfNeighbours = { new(0, 0), new(0, 1), new(1, -1), new(1, 0), new(1, 1) };

        void Separate()
        {
            foreach (var kv in _grid)
            {
                var bucket = kv.Value;
                foreach (var off in HalfNeighbours)
                {
                    bool same = off == Vector2Int.zero;
                    List<int> other = bucket;
                    if (!same && !_grid.TryGetValue(kv.Key + off, out other)) continue;
                    for (int a = 0; a < bucket.Count; a++)
                    {
                        int i = bucket[a];
                        for (int b = same ? a + 1 : 0; b < other.Count; b++)
                        {
                            int j = other[b];
                            var d = Pos[j] - Pos[i];
                            float rr = Radius(i) + Radius(j);
                            float l2 = d.sqrMagnitude;
                            if (l2 >= rr * rr) continue;
                            float l = Mathf.Sqrt(l2);
                            var nrm = l > 0.0001f ? d / l : Vector2.right;
                            var push = nrm * (rr - l) * 0.5f;
                            Pos[i] -= push;
                            Pos[j] += push;
                        }
                    }
                }
            }
        }
    }
}
