using System;

namespace ResidualSignal
{
    /// <summary>
    /// L4D2-style difficulty tiers and the mission picked on the start screen. Plain static state: the menu writes it
    /// once before deploying, gameplay reads the multipliers. 쉬움 is the pre-difficulty tuning; 보통 is the default.
    /// Command line: -difficulty 0..3, -mission campaign|annex|survival|scavenge.
    /// </summary>
    public static class Difficulty
    {
        public enum Level { Easy, Normal, Advanced, Expert }
        public enum Mission { Campaign, Annex, Survival, Scavenge }

        public static Level Current = Level.Normal;
        public static Mission Selected = Mission.Campaign;

        static readonly string[] LevelNames = { "쉬움", "보통", "고급", "전문가" };
        public static string LevelName => LevelNames[(int)Current];

        // Easy, Normal, Advanced, Expert.
        static readonly float[] HurtScale = { 1f, 1.35f, 1.9f, 2.7f };
        static readonly float[] HealthScale = { 1f, 1.2f, 1.45f, 1.75f };
        static readonly float[] HordeScale = { 1f, 1.3f, 1.65f, 2.1f };
        static readonly float[] MobIntervalScale = { 1f, 0.75f, 0.6f, 0.45f };
        static readonly float[] SpecialCooldownScale = { 1f, 0.8f, 0.6f, 0.45f };
        static readonly float[] AttackIntervalScale = { 1f, 0.9f, 0.8f, 0.7f };
        static readonly float[] BotDamageScale = { 1f, 0.8f, 0.65f, 0.5f };
        static readonly int[] ReviveHealth = { 40, 35, 30, 20 };
        // Share of a round's damage a teammate takes when you shoot them (L4D2 friendly fire).
        static readonly float[] FriendlyFireScale = { 0f, 0.1f, 0.3f, 0.6f };

        static int Index => (int)Current;

        /// <summary>Damage an infected deals to a survivor (player or bot).</summary>
        // ponytail: per-frame hold ticks (1-2) stay unscaled, rounding them up would triple pin damage; scale holds
        // through their per-second rate if pins need to be harsher on Expert.
        public static int Hurt(int damage) => damage <= 2 ? damage : (int)Math.Round(damage * HurtScale[Index]);
        public static int InfectedHealth(int health) => (int)Math.Round(health * HealthScale[Index]);
        public static int Horde(int count) => count <= 0 ? count : Math.Max(1, (int)Math.Round(count * HordeScale[Index]));
        public static float MobInterval => MobIntervalScale[Index];
        public static float SpecialCooldown => SpecialCooldownScale[Index];
        public static float AttackInterval => AttackIntervalScale[Index];
        public static float BotDamage => BotDamageScale[Index];
        public static int ReviveTo => ReviveHealth[Index];
        public static float FriendlyFire => FriendlyFireScale[Index];

        public static string MissionTitle => Selected switch
        {
            Mission.Annex => "정비동 돌파",
            Mission.Survival => "승강장 사수",
            Mission.Scavenge => "연료 수집",
            _ => "열한 번째 이름",
        };

        public static string MissionLine => Selected switch
        {
            Mission.Annex => "서이경을 데리고 정비동과 적재장을 지나 화물 연결로로 탈출하세요.",
            Mission.Survival => "끝없이 밀려오는 무리 속에서 최대한 오래 버티세요.",
            Mission.Scavenge => "흩어진 연료통을 발전기로 옮기세요. 통마다 시간이 늘어납니다.",
            _ => "역에 남은 구조 요청을 확인하세요.",
        };

        /// <summary>L4D2 Survival medals: bronze 4:00, silver 7:00, gold 10:00.</summary>
        public static string Medal(float seconds) =>
            seconds >= 600f ? "금메달" : seconds >= 420f ? "은메달" : seconds >= 240f ? "동메달" : "메달 없음";

        public static void ReadCommandLine(string[] arguments)
        {
            int at = Array.IndexOf(arguments, "-difficulty");
            if (at >= 0 && at + 1 < arguments.Length && int.TryParse(arguments[at + 1], out int level))
                Current = (Level)Math.Clamp(level, 0, 3);
            at = Array.IndexOf(arguments, "-mission");
            if (at >= 0 && at + 1 < arguments.Length)
                Selected = arguments[at + 1] switch
                {
                    "annex" => Mission.Annex,
                    "survival" => Mission.Survival,
                    "scavenge" => Mission.Scavenge,
                    _ => Mission.Campaign,
                };
        }
    }
}
