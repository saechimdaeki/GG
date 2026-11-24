using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Breaks up the flat box walls: rubber skirting along the floor, a concrete cornice where wall meets
    /// ceiling, shallow structural pilasters on long runs and frames around doorway-sized openings.
    /// Visual only (no colliders), so navigation and cover are unchanged. Called from LivedIn before wall
    /// services so fixtures keep clear of the new trim.
    /// </summary>
    public static partial class StationSceneBuilder
    {
        const float PilasterSpacing = 4.5f;

        struct WallRun
        {
            public float start, end, x;
            public float Length => end - start;
        }

        static int WallStructure(Transform root, List<Section> sections, List<Bounds> fittings)
        {
            var trim = Surface("Trim - dark rubber", new Color(0.06f, 0.06f, 0.06f), 0.3f);
            var concrete = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Ceiling - cast concrete.mat");
            var steel = AssetDatabase.LoadAssetAtPath<Material>($"{SettingsFolder}/Steel - powder coat.mat");
            var structure = new GameObject("Wall structure").transform;
            structure.SetParent(root, false);
            int placed = 0;
            foreach (int side in new[] { -1, 1 })
            {
                // Skirting follows the wall at shin height; cornice follows it just under the ceiling.
                foreach (var run in Runs(sections, side, s => SolidWallX(s, side, 0.12f)))
                {
                    Place(structure, "Skirting", new Vector3(run.x - side * 0.02f, Floor(sections, run) + 0.08f, (run.start + run.end) * 0.5f),
                        new Vector3(0.04f, 0.16f, run.Length), trim, fittings);
                    placed++;
                }
                foreach (var run in Runs(sections, side, s => side < 0 ? s.left : s.right))
                {
                    float ceiling = sections.Where(s => s.valid && s.z >= run.start && s.z <= run.end).Min(s => s.ceiling);
                    Place(structure, "Cornice", new Vector3(run.x - side * 0.07f, ceiling - 0.12f, (run.start + run.end) * 0.5f),
                        new Vector3(0.14f, 0.24f, run.Length), concrete, fittings);
                    placed++;
                }
                // Pilasters only where the wall is one plane from floor to ceiling.
                var full = Runs(sections, side, s =>
                {
                    float? low = SolidWallX(s, side, 0.4f), mid = SolidWallX(s, side, 1.8f);
                    float high = side < 0 ? s.left : s.right;
                    return low.HasValue && mid.HasValue && Mathf.Abs(low.Value - high) < 0.05f && Mathf.Abs(mid.Value - high) < 0.05f
                        ? high : null;
                });
                foreach (var run in full.Where(r => r.Length >= 3f))
                {
                    for (float z = run.start + 1.2f; z <= run.end - 1.2f; z += PilasterSpacing)
                    {
                        var section = sections.OrderBy(s => Mathf.Abs(s.z - z)).First();
                        float height = section.ceiling - section.floor - 0.24f;
                        var centre = new Vector3(run.x - side * 0.065f, section.floor + height * 0.5f, z);
                        var size = new Vector3(0.13f, height, 0.5f);
                        if (fittings.Any(f => f.Intersects(new Bounds(centre, size + new Vector3(0.1f, -0.4f, 0.3f)))))
                            continue;
                        Place(structure, "Pilaster", centre, size, concrete, fittings);
                        placed++;
                    }
                }
                // Doorway-sized gaps in the mid-height wall get a steel frame on both jambs and a header.
                var mids = Runs(sections, side, s => SolidWallX(s, side, 1.2f));
                for (int i = 1; i < mids.Count; i++)
                {
                    WallRun before = mids[i - 1], after = mids[i];
                    float gap = after.start - before.end;
                    if (gap < 0.8f || gap > 3.6f || Mathf.Abs(before.x - after.x) > 0.15f)
                        continue;
                    // A real opening goes deeper than the wall; a cabinet or column in front of it does not.
                    bool open = sections.Where(s => s.valid && s.z > before.end + 0.1f && s.z < after.start - 0.1f).All(s =>
                        !Physics.Raycast(new Vector3(s.x, s.floor + 1.2f, s.z), Vector3.right * side, out var probe, 9f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || side * (probe.point.x - before.x) > 0.3f);
                    if (!open)
                        continue;
                    float x = before.x, top = OpeningTop(new Vector3(before.x + side * 0.12f, Floor(sections, before) + 0.05f, (before.end + after.start) * 0.5f));
                    if (top < 1.9f)
                        continue;
                    foreach (float edge in new[] { before.end + 0.1f, after.start - 0.1f })
                        Place(structure, "Door jamb", new Vector3(x - side * 0.08f, top * 0.5f, edge), new Vector3(0.16f, top, 0.14f), steel, null);
                    Place(structure, "Door header", new Vector3(x - side * 0.08f, top + 0.07f, (before.end + after.start) * 0.5f),
                        new Vector3(0.16f, 0.14f, gap + 0.06f), steel, null);
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>x of a real, visible solid wall at this height (no glass, doors or shutters), else null.</summary>
        static float? SolidWallX(Section section, int side, float height)
        {
            if (!section.valid || !WallAt(section, side, height, out var wall))
                return null;
            return wall.point.x;
        }

        /// <summary>Contiguous stretches where wallX stays within 12 cm; breaks at columns, openings and steps.</summary>
        static List<WallRun> Runs(List<Section> sections, int side, Func<Section, float?> wallX)
        {
            var runs = new List<WallRun>();
            WallRun? current = null;
            foreach (var section in sections)
            {
                float? x = section.valid ? wallX(section) : null;
                if (current is { } run && x.HasValue && Mathf.Abs(x.Value - run.x) < 0.12f)
                {
                    current = new WallRun { start = run.start, end = section.z, x = run.x };
                    continue;
                }
                if (current is { } done && done.Length >= 1.5f)
                    runs.Add(done);
                current = x.HasValue ? new WallRun { start = section.z, end = section.z, x = x.Value } : null;
            }
            if (current is { } last && last.Length >= 1.5f)
                runs.Add(last);
            // Half a step either side so trims meet the next piece instead of stopping short.
            return runs.Select(r => new WallRun { start = r.start - DetailStep * 0.5f, end = r.end + DetailStep * 0.5f, x = r.x }).ToList();
        }

        static float Floor(List<Section> sections, WallRun run) =>
            sections.Where(s => s.valid && s.z >= run.start && s.z <= run.end).Select(s => s.floor).DefaultIfEmpty(0f).Min();

        static float OpeningTop(Vector3 floorPoint) =>
            Physics.Raycast(floorPoint, Vector3.up, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Mathf.Min(hit.point.y, 2.6f) : 2.4f;

        static void Place(Transform parent, string name, Vector3 centre, Vector3 size, Material material, List<Bounds> fittings)
        {
            Shape(parent, PrimitiveType.Cube, name, centre, size, material);
            fittings?.Add(new Bounds(centre, size));
        }
    }
}
