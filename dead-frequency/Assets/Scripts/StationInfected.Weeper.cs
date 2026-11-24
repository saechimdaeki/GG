using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// 흐느끼는 자 (an L4D2 Witch-style infected): slumped on the floor sobbing, deaf to gunfire. Light on her face,
    /// standing close or any wound builds her rage; once it fills she screams and sprints at the player with a
    /// strike that can down a survivor outright. Solo only, spawned by the mission, not part of the director's horde.
    /// </summary>
    public sealed partial class StationInfected
    {
        const float WeeperStrike = 58f;

        bool weeping;
        float rage;
        int weeperStartHealth;
        int weeperStrike;
        float nextSob;

        public bool Weeping => weeping;
        public float Rage => rage;
        public bool IsWeeper => weeperStrike > 0;

        /// <summary>Checks only: why she is or isn't swinging.</summary>
        public string WeeperDebug => $"windup {windup:F2} cooldown {attackCooldown:F2} stagger {stagger:F2} hold {holdUntil - Time.time:F2} " +
            $"target {(target != null && target.body != null ? target.body.name : "none")} closeIn {Scaled(closeIn):F2} shove {shoveVelocity.magnitude:F2} enabled {enabled}";

        /// <summary>Before Follow: she must keep a standing body (no crawler roll).</summary>
        public void MarkWeeper() => weeperStrike = Mathf.RoundToInt(WeeperStrike);

        /// <summary>Put this calm common into the weeping pose. Call after Follow(calm: true).</summary>
        public void BeginWeeping(int startHealth)
        {
            weeping = true;
            alerted = false;
            rage = 0f;
            health = weeperStartHealth = startHealth;
            weeperStrike = Mathf.RoundToInt(WeeperStrike);
            // Long claws: some bodies could not close inside the default swing range against a standing player.
            closeIn = Mathf.Max(closeIn, 1.45f);
            reach = Mathf.Max(reach, 1.8f);
            if (riseClip != null)
            {
                // Frozen early in the get-up clip: hunched over her knees.
                rig["rise"].time = rig["rise"].length * 0.22f;
                rig["rise"].speed = 0f;
                rig.Play("rise");
            }
        }

        /// <summary>Called from Update in place of the normal brain while she sobs.</summary>
        void Weep(float delta)
        {
            if (Time.time >= nextSob)
            {
                nextSob = Time.time + Random.Range(2.2f, 3.4f);
                Voice(groans, 0.32f);
            }
            var player = StationTrial.Local;
            if (player == null)
                return;
            Vector3 toHer = transform.position + Vector3.up * 0.8f - player.Eyes.position;
            float distance = toHer.magnitude;
            float anger = 0f;
            // Close enough to touch her and she wakes in about a second; passing at arm's length is a gamble.
            if (distance < 5f)
                anger += (1f - distance / 5f) * 1.4f;
            if (player.FlashlightOn && distance < 11f && Vector3.Angle(player.Eyes.forward, toHer) < 14f)
                anger += 0.9f;
            rage = anger > 0f ? rage + anger * delta : Mathf.Max(0f, rage - 0.2f * delta);
            if (health < weeperStartHealth || rage >= 1f)
                Startle();
        }

        void Startle()
        {
            if (!weeping)
                return;
            weeping = false;
            rage = 1f;
            runner = true;
            mission.TrackThreat(this);
            // Let Alert play the get-up clip from her hunched pose.
            lyingDown = true;
            Alert();
            Voice(screams, 1f);
            mission.WarnWeeper();
        }

        int StrikeDamage => weeperStrike > 0 ? weeperStrike : runner ? 19 : attackDamage;
    }
}
