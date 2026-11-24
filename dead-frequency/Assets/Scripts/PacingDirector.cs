using System;

namespace ResidualSignal
{
    /// <summary>
    /// L4D-style pacing: stress (intensity) rises when the squad gets hurt or fights up close and bleeds off when
    /// no enemy is near. Build-up sends a mob every so often; hitting full intensity holds a short peak, then a
    /// relax window with no mobs, then build-up again. Plain C# so it can be checked without a scene.
    /// </summary>
    public sealed class PacingDirector
    {
        public enum Mode { BuildUp, Peak, Relax }

        public float Intensity { get; private set; }
        public Mode State { get; private set; } = Mode.BuildUp;

        readonly float mobMin, mobMax, relaxSeconds, peakSeconds, decayPerSecond;
        readonly Random random;
        float stateTimer;
        float mobTimer;

        public PacingDirector(float mobMin, float mobMax, float relaxSeconds, float peakSeconds, float decayPerSecond, int seed)
        {
            this.mobMin = mobMin;
            this.mobMax = mobMax;
            this.relaxSeconds = relaxSeconds;
            this.peakSeconds = peakSeconds;
            this.decayPerSecond = decayPerSecond;
            random = new Random(seed);
            mobTimer = NextMob();
        }

        /// <summary>Damage taken, a close kill, a teammate going down. 1 = instant peak.</summary>
        public void AddStress(float amount) => Intensity = Math.Clamp(Intensity + amount, 0f, 1f);

        /// <summary>Advance time. Returns true when a mob should be sent now.</summary>
        /// <param name="enemiesNear">Alerted infected close to the squad; intensity only decays without them.</param>
        /// <param name="blocked">A scripted event or another horde is running; the mob clock waits.</param>
        public bool Tick(float delta, bool enemiesNear, bool blocked)
        {
            if (!enemiesNear)
                Intensity = Math.Max(0f, Intensity - decayPerSecond * delta);
            switch (State)
            {
                case Mode.BuildUp when Intensity >= 1f:
                    State = Mode.Peak;
                    stateTimer = peakSeconds;
                    return false;
                case Mode.Peak:
                    stateTimer -= delta;
                    if (stateTimer <= 0f)
                    {
                        State = Mode.Relax;
                        stateTimer = relaxSeconds;
                    }
                    return false;
                case Mode.Relax:
                    // The relax clock only runs once the fight is actually over.
                    if (!enemiesNear)
                        stateTimer -= delta;
                    if (stateTimer <= 0f)
                    {
                        State = Mode.BuildUp;
                        mobTimer = NextMob();
                    }
                    return false;
            }
            if (blocked)
                return false;
            mobTimer -= delta;
            if (mobTimer > 0f)
                return false;
            mobTimer = NextMob();
            return true;
        }

        float NextMob() => mobMin + (float)random.NextDouble() * (mobMax - mobMin);
    }
}
