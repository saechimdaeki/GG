using UnityEngine;

namespace ResidualSignal
{
    /// <summary>
    /// Left 4 Dead's extra slots: a throwable (G or 3), pills or adrenaline (4), and a melee weapon in place of the pistol (2).
    /// Pills and adrenaline give temporary health that bleeds away and is lost first when hit.
    /// </summary>
    public sealed partial class StationTrial
    {
        const float ThrowSpeed = 15f;
        public const float TempDecayPerSecond = 0.27f;
        public const float AdrenalineSeconds = 15f;

        Gear? carriedThrowable;
        Gear? carriedMedicine;
        float nextThrowAt;
        float tempHealth;
        float adrenalineUntil;

        public Gear? CarriedThrowable => carriedThrowable;
        public Gear? CarriedMedicine => carriedMedicine;
        public float TempHealth => tempHealth;
        public bool Adrenaline => Time.time < adrenalineUntil;
        /// <summary>Adrenaline: faster on your feet and quicker hands.</summary>
        float GearSpeed => Adrenaline ? 1.25f : 1f;

        public void GiveGear(Gear gear)
        {
            if (gear.IsThrowable()) carriedThrowable = gear;
            else if (gear.IsMedicine()) carriedMedicine = gear;
            else TakeMelee(gear);
        }

        /// <summary>Lob the carried throwable along the view. Returns it, or null when there is nothing to throw.
        /// In co-op every peer gets the same throw; the host's copy is the one that hurts and lures.</summary>
        public Throwable TryThrow()
        {
            if (carriedThrowable == null || Time.time < nextThrowAt || Downed || Held || !InCombat)
                return null;
            var kind = carriedThrowable.Value.AsThrowable();
            carriedThrowable = null;
            nextThrowAt = Time.time + 0.8f;
            var view = eyes.transform;
            Vector3 origin = view.position + view.forward * 0.45f + view.right * 0.12f - view.up * 0.08f;
            Vector3 velocity = view.forward * ThrowSpeed + Vector3.up * 3.2f + player.velocity;
            Signal(kind == Throwable.Kind.PipeBomb ? "파이프 폭탄 투척!" : "화염병 투척!", 2f);
            raise = 1f; // the gun dips out of the way for the throw and comes back up
            if (Networked)
                MissionSync.Instance?.ThrowRpc((byte)kind, origin, velocity);
            return Throwable.Launch(kind, origin, velocity, BoomClip(), Authority);
        }

        internal AudioClip BoomClip()
        {
            foreach (var gun in arsenal)
                if (gun.pellets > 1 && gun.shot != null)
                    return gun.shot;
            return arsenal[0].shot;
        }

        /// <summary>4: swallow the pills or jab the adrenaline.</summary>
        public bool TryUseMedicine()
        {
            if (carriedMedicine == null || Downed || Held || !InCombat)
                return false;
            var gear = carriedMedicine.Value;
            if (gear == Gear.Pills && health + tempHealth >= 99f)
            {
                Signal("지금은 진통제가 필요 없다", 2f);
                return false;
            }
            carriedMedicine = null;
            AddTempHealth(gear == Gear.Pills ? 50f : 25f);
            if (gear == Gear.Adrenaline)
            {
                adrenalineUntil = Time.time + AdrenalineSeconds;
                Signal("아드레날린 · 15초간 빨라진다", 2.5f);
            }
            else
                Signal("진통제를 삼켰다", 2f);
            raise = 1f;
            return true;
        }

        void AddTempHealth(float amount) => tempHealth = Mathf.Min(100f - health, tempHealth + amount);

        /// <summary>Temporary health soaks a hit first; returns what is left for real health.</summary>
        int AbsorbWithTempHealth(int damage)
        {
            float soaked = Mathf.Min(tempHealth, damage);
            tempHealth -= soaked;
            return damage - Mathf.FloorToInt(soaked);
        }

        void UpdateGear()
        {
            tempHealth = Mathf.Max(0f, tempHealth - TempDecayPerSecond * Time.deltaTime);
            if (health + tempHealth > 100f)
                tempHealth = 100f - health;
            UpdateMelee();
        }

        GearPickup NearbyGear()
        {
            GearPickup closest = null;
            float nearest = 1.5f * 1.5f;
            foreach (var pickup in GearPickup.All)
            {
                if (Carrying(pickup.gear))
                    continue;
                float distance = (pickup.transform.position - transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    closest = pickup;
                }
            }
            return closest;
        }

        bool Carrying(Gear gear) => carriedThrowable == gear || carriedMedicine == gear || (melee != null && melee.gear == gear);

        /// <summary>E near gear: take it, leaving the one you carried in that slot in its place.</summary>
        bool TryTakeGear()
        {
            var pickup = NearbyGear();
            if (pickup == null)
                return false;
            var gear = pickup.gear;
            Gear? dropped = gear.IsThrowable() ? carriedThrowable : gear.IsMedicine() ? carriedMedicine : melee?.gear;
            Vector3 spot = pickup.transform.position;
            Destroy(pickup.gameObject);
            GiveGear(gear);
            Signal(gear.IsThrowable() ? $"{gear.LabelObject()} 챙겼다 · G로 투척" :
                gear.IsMedicine() ? $"{gear.LabelObject()} 챙겼다 · 4로 사용" : $"{gear.LabelObject()} 들었다 · 2로 꺼내기", 2.5f);
            if (dropped != null)
                PlaceGear(dropped.Value, spot);
            return true;
        }

        /// <summary>The blast rattles the camera of anyone close enough to feel it.</summary>
        public void FeelExplosion(Vector3 at, float radius)
        {
            float distance = Vector3.Distance(at, transform.position);
            if (distance < radius)
                shake = Mathf.Max(shake, 2.6f * (1f - distance / radius));
        }

        static readonly Gear[] MeleeSpread = { Gear.FireAxe, Gear.Bat, Gear.Crowbar, Gear.Machete };

        /// <summary>
        /// Gear around the supplies: a throwable beside each ammo box, pills or adrenaline beside each first-aid kit,
        /// and the four melee weapons spread over the first few of both.
        /// </summary>
        void PlaceGearAroundSupplies()
        {
            int ammo = 0, aid = 0, melees = 0;
            foreach (var loot in LootItem.All.ToArray())
            {
                Vector3 right = loot.transform.position + loot.transform.right * 0.55f;
                Vector3 left = loot.transform.position - loot.transform.right * 0.7f;
                if (loot.kind == LootItem.Kind.Ammo)
                    PlaceGear(ammo++ % 2 == 0 ? Gear.PipeBomb : Gear.Molotov, right);
                else
                    PlaceGear(aid++ % 2 == 0 ? Gear.Pills : Gear.Adrenaline, right);
                if (melees < MeleeSpread.Length && (ammo + aid) % 2 == 1)
                    PlaceGear(MeleeSpread[melees++], left);
            }
        }

        static void PlaceGear(Gear gear, Vector3 point)
        {
            var model = GearInfo.Model(gear, point, Random.Range(0f, 360f));
            model.AddComponent<GearPickup>().gear = gear;
            // A faint lamp so it can be spotted in the dark, like Left 4 Dead's item glow.
            var lamp = new GameObject("Pickup glow").AddComponent<Light>();
            lamp.transform.SetParent(model.transform, false);
            lamp.transform.position = point + Vector3.up * 0.35f;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.88f, 0.7f);
            lamp.range = 1.4f;
            lamp.intensity = 18f;
            lamp.shadows = LightShadows.None;
        }
    }
}
