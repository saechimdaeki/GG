using UnityEngine;

namespace ResidualSignal
{
    /// <summary>The melee weapon in the secondary slot: drawn with 2, swung with the fire button.</summary>
    public sealed partial class StationTrial
    {
        const float SwingWindup = 0.1f;
        const float SwingStrike = 0.13f;
        const float SwingRecover = 0.32f;

        MeleeWeapon melee;
        Transform meleeView;
        bool meleeOut;
        float swingStarted = -10f;
        bool swingLanded;
        float nextSwingAt;
        static AudioClip whoosh;

        public bool MeleeOut => meleeOut;
        public Gear? MeleeGear => melee?.gear;

        void TakeMelee(Gear gear)
        {
            bool wasOut = meleeOut || equipped == 0;
            if (meleeView != null)
                Destroy(meleeView.gameObject);
            melee = MeleeWeapon.For(gear);
            meleeView = MeleeWeapon.Model(gear).transform;
            Destroy(meleeView.GetComponent<Collider>());
            meleeView.SetParent(viewLens, false);
            meleeView.localScale = Vector3.one * 0.8f;
            // The weapon sits behind the flashlight's lens; a soft lamp at the hand keeps it from reading as a silhouette.
            var fill = new GameObject("Melee fill").AddComponent<Light>();
            fill.transform.SetParent(meleeView, false);
            fill.transform.localPosition = new Vector3(-0.25f, 0.35f, 0.1f);
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.9f, 0.78f);
            fill.range = 1.1f;
            fill.intensity = 4f;
            fill.shadows = LightShadows.None;
            foreach (var part in meleeView.GetComponentsInChildren<Renderer>())
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meleeView.gameObject.SetActive(false);
            meleeOut = false;
            // The melee weapon takes the pistol's place: if the pistol was in hand, the new weapon is now.
            if (wasOut)
                DrawMelee();
        }

        /// <summary>2: put the gun away and bring the melee weapon up.</summary>
        public void DrawMelee()
        {
            if (melee == null || meleeOut)
                return;
            Current.view.gameObject.SetActive(false);
            Current.muzzle.SetActive(false);
            reloadUntil = 0;
            meleeOut = true;
            raise = 1f;
            meleeView.gameObject.SetActive(true);
        }

        void HolsterMelee()
        {
            if (!meleeOut)
                return;
            meleeOut = false;
            meleeView.gameObject.SetActive(false);
        }

        /// <summary>Slot 2: the melee weapon if carried, else the pistol.</summary>
        void EquipSecondary()
        {
            if (melee != null)
                DrawMelee();
            else
                Equip(0);
        }

        /// <summary>Mouse wheel: swap between the primary and the secondary slot.</summary>
        void CycleWeapon()
        {
            bool secondaryOut = meleeOut || equipped == 0;
            if (secondaryOut && primary >= 0)
                Equip(primary);
            else if (!secondaryOut)
                EquipSecondary();
        }

        /// <summary>Fire button with the melee weapon out. Returns true when a swing starts.</summary>
        public bool TrySwing()
        {
            if (!meleeOut || pinnedBy != null || Downed || launchedByImpact || Time.time < knockLandingAt ||
                phase is Phase.Complete or Phase.Failed || Time.time < nextSwingAt || raise > 0.35f)
                return false;
            float pace = Adrenaline ? 0.75f : 1f;
            nextSwingAt = Time.time + melee.interval * pace;
            swingStarted = Time.time;
            swingLanded = false;
            weaponAudio.pitch = Random.Range(0.9f, 1.1f);
            weaponAudio.PlayOneShot(whoosh ??= ShotFeedback.Tick("Melee whoosh", 220f, 0.22f, 0.35f, sweep: -0.45f), 0.8f);
            return true;
        }

        /// <summary>At the strike: cut through the nearest infected in front, up to the weapon's cleave.</summary>
        void LandSwing()
        {
            swingLanded = true;
            Vector3 origin = eyes.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up).normalized;
            var struck = new System.Collections.Generic.List<(StationInfected infected, float distance)>();
            foreach (var infected in StationInfected.Living)
            {
                if (infected == null || infected.Dead)
                    continue;
                Vector3 chest = infected.transform.position + Vector3.up * 1.1f;
                Vector3 to = chest - origin;
                float flat = Vector3.ProjectOnPlane(to, Vector3.up).magnitude;
                if (flat > melee.reach + 0.35f || Mathf.Abs(to.y) > 1.6f)
                    continue;
                if (flat > 0.4f && Vector3.Angle(forward, Vector3.ProjectOnPlane(to, Vector3.up)) > 55f)
                    continue;
                if (CoveredByMap(origin, chest))
                    continue;
                struck.Add((infected, flat));
            }
            struck.Sort((a, b) => a.distance.CompareTo(b.distance));
            int hits = 0;
            foreach (var (infected, _) in struck)
            {
                if (hits++ >= melee.cleave)
                    break;
                Vector3 point = infected.transform.position + Vector3.up * 1.3f;
                // The tank shrugs off a swing; everything else feels it in full.
                int damage = infected.Kind == InfectedKind.Brute ? melee.damage / 4 : melee.damage;
                Damage(infected, point, damage);
                ShotFeedback.BloodMist(point, eyes.transform.right, damage);
            }
            if (hits > 0)
            {
                if (fleshSound != null)
                    weaponAudio.PlayOneShot(fleshSound, 0.9f);
                shake = Mathf.Max(shake, 0.9f);
            }
            else if (Physics.Raycast(origin, eyes.transform.forward, out RaycastHit wall, melee.reach,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && IsImpactSurface(wall.collider))
            {
                ShotFeedback.Impact(wall.point, wall.normal);
                wall.collider.GetComponentInParent<CarAlarm>()?.Trip();
            }
            if (Authority)
                StationInfected.Noise(transform.position, 8f);
        }

        void UpdateMelee()
        {
            if (!meleeOut || meleeView == null)
                return;
            float t = Time.time - swingStarted;
            if (!swingLanded && t >= SwingWindup + SwingStrike * 0.5f && t < SwingWindup + SwingStrike + SwingRecover)
                LandSwing();
            // Rest: held low on the right, head leaning in. Swing: cocked over the shoulder, then chopped down and across.
            var rest = (position: new Vector3(0.3f, -0.36f, 0.58f), angles: new Vector3(20f, 0f, 8f));
            var cocked = (position: new Vector3(0.38f, -0.18f, 0.45f), angles: new Vector3(-25f, 0f, -10f));
            var struckThrough = (position: new Vector3(-0.02f, -0.42f, 0.6f), angles: new Vector3(85f, 0f, 55f));
            Vector3 position;
            Quaternion rotation;
            if (t < SwingWindup)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / SwingWindup);
                position = Vector3.Lerp(rest.position, cocked.position, k);
                rotation = Quaternion.Slerp(Quaternion.Euler(rest.angles), Quaternion.Euler(cocked.angles), k);
            }
            else if (t < SwingWindup + SwingStrike)
            {
                float k = (t - SwingWindup) / SwingStrike;
                position = Vector3.Lerp(cocked.position, struckThrough.position, k);
                rotation = Quaternion.Slerp(Quaternion.Euler(cocked.angles), Quaternion.Euler(struckThrough.angles), k);
            }
            else if (t < SwingWindup + SwingStrike + SwingRecover)
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - SwingWindup - SwingStrike) / SwingRecover);
                position = Vector3.Lerp(struckThrough.position, rest.position, k);
                rotation = Quaternion.Slerp(Quaternion.Euler(struckThrough.angles), Quaternion.Euler(rest.angles), k);
            }
            else
            {
                float steady = strideWeight;
                position = rest.position + new Vector3(Mathf.Sin(stride * 0.5f) * 0.012f, Mathf.Sin(stride) * 0.014f, 0f) * steady;
                rotation = Quaternion.Euler(rest.angles);
            }
            meleeView.localPosition = position + Vector3.down * raise * 0.2f;
            meleeView.localRotation = rotation;
        }
    }
}
