using System;
using System.Collections.Generic;

namespace ReelDungeon.Core
{
    public enum NodeType { Combat, Elite, Recruit, Shop, Rest, Treasure, Event, Boss }

    /// <summary>One stop on a chapter map.</summary>
    public class MapNode
    {
        public int Row, Lane;
        public NodeType Type;
        public bool Visited;
        public List<MapNode> Next = new List<MapNode>();
    }

    /// <summary>
    /// A chapter map: rows of three lanes, each node linking to the same or a neighbouring lane
    /// in the next row. Row 0 is all fights, the row before the boss has one rest, the last row is the boss.
    /// </summary>
    public class StageMap
    {
        public const int Lanes = 3;
        public List<MapNode[]> Rows { get; } = new List<MapNode[]>();
        public MapNode Boss => Rows[Rows.Count - 1][0];

        public static StageMap Generate(TuningConfig cfg, Random rng)
        {
            var map = new StageMap();
            int rows = Math.Max(3, cfg.mapRows);
            for (int r = 0; r < rows - 1; r++)
            {
                var row = new MapNode[Lanes];
                for (int l = 0; l < Lanes; l++)
                {
                    var type = r == 0 ? NodeType.Combat
                        : (NodeType)Enum.Parse(typeof(NodeType), cfg.nodePool[rng.Next(cfg.nodePool.Length)]);
                    row[l] = new MapNode { Row = r, Lane = l, Type = type };
                }
                map.Rows.Add(row);
            }
            // a recruit early, so the party can grow before the first boss
            if (rows > 3) map.Rows[1][rng.Next(Lanes)].Type = NodeType.Recruit;
            // one rest before the boss, so the HP you arrive with matters
            map.Rows[rows - 2][rng.Next(Lanes)].Type = NodeType.Rest;
            map.Rows.Add(new[] { new MapNode { Row = rows - 1, Lane = 1, Type = NodeType.Boss } });

            for (int r = 0; r < rows - 1; r++)
                foreach (var n in map.Rows[r])
                {
                    if (r == rows - 2) { n.Next.Add(map.Boss); continue; }
                    for (int l = Math.Max(0, n.Lane - 1); l <= Math.Min(Lanes - 1, n.Lane + 1); l++)
                        n.Next.Add(map.Rows[r + 1][l]);
                }
            return map;
        }
    }
}
