using System.Collections.Generic;
using System.Linq;

namespace ReelDungeon.Core
{
    /// <summary>What one spin (plus any armed skills) does this turn.</summary>
    public class SpinOutcome
    {
        public int damage, block, heal, selfDamage, weaken, gold, poison;
        public bool frozen;
        public string combo = "";
        public bool badCombo;
    }

    /// <summary>Turns the faces showing on the reels into damage, block, heal and self damage.</summary>
    public static class SpinResolver
    {
        /// <summary>Same symbol twice uses pairMultiplier, three or more times tripleMultiplier.</summary>
        public static SpinOutcome Resolve(IReadOnlyList<SymbolDef> faces, TuningConfig cfg)
        {
            var o = new SpinOutcome();
            foreach (var g in faces.GroupBy(f => f.id))
            {
                int n = g.Count();
                float m = n >= 3 ? cfg.tripleMultiplier : n == 2 ? cfg.pairMultiplier : 1f;
                var s = g.First();
                o.damage += (int)(s.damage * n * m);
                o.block += (int)(s.block * n * m);
                o.heal += (int)(s.heal * n * m);
                o.selfDamage += (int)(s.selfDamage * n * m);
                o.weaken += (int)(s.weaken * n * m);
                o.gold += (int)(s.gold * n * m);
                o.poison += (int)(s.poison * n * m);
                if (n >= 2)
                {
                    o.combo += $"{s.name} x{n}! ";
                    o.badCombo |= s.curse;
                }
            }
            return o;
        }
    }
}
