using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    public partial class Game
    {
        struct AttackCue { public float remaining; public bool winding; public Vector2 origin, direction; }
        readonly Dictionary<int, AttackCue> _incoming = new();
        readonly List<int> _attackKeys = new(), _attackNearby = new();
        readonly List<Vector3> _attackWarnings = new();
        readonly HashSet<int> _windingZombies = new();
        CombatAudio _combatAudio;
        Vector2 _lastDamageSource;
        float _damageCue, _playerRecoil, _impactSoundGate;

        internal const float AttackWindup = 0.43f;
        internal const float AttackRecovery = 0.78f;
        const float StrikeReach = 1.26f;

        void TickIncomingAttacks(float dt)
        {
            _impactSoundGate = Mathf.Max(0f, _impactSoundGate - dt);
            _damageCue = Mathf.Max(0f, _damageCue - dt);
            _playerRecoil = Mathf.Max(0f, _playerRecoil - dt);
            _windingZombies.Clear();
            if (InPractice || !Me.Alive) return;

            _attackKeys.Clear();
            _attackKeys.AddRange(_incoming.Keys);
            foreach (int id in _attackKeys)
            {
                AttackCue cue = _incoming[id];
                int zombie = Horde.Id.IndexOf(id);
                if (zombie < 0 || Horde.Hp[zombie] <= 0)
                {
                    _incoming.Remove(id);
                    continue;
                }
                if (cue.winding && Horde.Flash[zombie] > 0)
                {
                    _incoming[id] = new AttackCue { remaining = AttackRecovery };
                    continue;
                }
                cue.remaining -= dt;
                if (cue.remaining > 0)
                {
                    _incoming[id] = cue;
                    if (cue.winding) _windingZombies.Add(id);
                    continue;
                }
                if (!cue.winding)
                {
                    _incoming.Remove(id);
                    continue;
                }

                Vector2 toward = Me.Pos - cue.origin;
                if (toward.sqrMagnitude <= StrikeReach * StrikeReach &&
                    Vector2.Angle(cue.direction, toward) <= 27f && CanReachForStealth(Me.Pos, Horde.Pos[zombie]))
                    HurtPlayer(Me, Horde.Type(zombie).dps * (AttackWindup + AttackRecovery), Horde.Pos[zombie]);
                _incoming[id] = new AttackCue { remaining = AttackRecovery };
            }

            foreach (int i in Horde.Near(Me.Pos, 0.82f, _attackNearby))
            {
                if (!Horde.IsAware(i) || Horde.Hp[i] <= 0 || Horde.Flash[i] > 0 ||
                    _incoming.ContainsKey(Horde.Id[i]) || !CanReachForStealth(Me.Pos, Horde.Pos[i])) continue;
                Vector2 from = Horde.Pos[i];
                Vector2 dir = (Me.Pos - from).normalized;
                _incoming[Horde.Id[i]] = new AttackCue { remaining = AttackWindup, winding = true, origin = from, direction = dir };
                _windingZombies.Add(Horde.Id[i]);
                _fx.Fan(from, dir, StrikeReach, 0.45f, new Color(1f, 0.34f, 0.16f, 0.88f), AttackWindup);
            }
        }

        void HurtPlayer(Survivor player, float amount, Vector2 from)
        {
            if (InPractice || amount <= 0f || !player.Alive) return;
            float before = player.Hp;
            player.Hurt(amount);
            if (player != Me || Mathf.Approximately(before, player.Hp)) return;
            if (Sleeping) Wake("공격을 받아 잠에서 깼다");
            if (_lootSource != null) CloseLoot();
            _lastDamageSource = from;
            if (_damageCue <= 0.34f) _combatAudio.Play("hurt");
            _damageCue = 0.55f;
            _playerRecoil = 0.20f;
            PopText(player.Pos + Vector2.down * 0.34f, $"-{Mathf.Max(1, Mathf.RoundToInt(before - player.Hp))}",
                new Color(1f, 0.37f, 0.27f), -36f);
            _fx.BloodBurst(player.Pos, (player.Pos - from).normalized, 5);
        }

        public void ReportImpact(Vector2 position, Vector2 direction, float amount, bool fatal, bool silent)
        {
            if (_fx == null) return;
            _fx.BloodBurst(position, direction, silent ? 16 : fatal ? 14 : 8);
            _fx.HitSparks(position, new Color(1f, 0.85f, 0.6f), fatal ? 8 : 5);
            if (fatal) _fx.BloodPool(position + direction * 0.4f, silent ? 1.1f : 0.8f);
            _fx.Ring(position, fatal ? 0.8f : 0.55f, new Color(1f, 0.69f, 0.40f, 0.80f), 0.19f);
            string label = silent ? "암살" : Mathf.CeilToInt(amount).ToString();
            PopText(position, label, fatal ? new Color(1f, 0.67f, 0.36f) : new Color(1f, 0.93f, 0.77f), 32f);
            if (_impactSoundGate > 0) return;
            _combatAudio.Play(silent ? "stealth" : "impact");
            if (!silent && Me.Held.id is "pipe" or "axe" or "bat") _combatAudio.Play("metal", 0.5f);
            _impactSoundGate = 0.08f;
        }

        void SetupCombatCapture(string capture)
        {
            Me.Pos = City.MarketSquare + Vector2.down * 0.6f;
            Me.Take("pipe", 1);
            var attacker = Me.Pos + Vector2.up * 0.86f;
            Horde.Spawn(attacker, 0);
            Horde.Alert(Horde.Count - 1, Me.Pos, 15f);
            Horde.Step(0f, Players.ConvertAll(player => player.Pos));
            float hp = Me.Hp;
            TickIncomingAttacks(0f);
            if (Me.Hp != hp || _incoming.Count == 0)
                throw new System.InvalidOperationException("Incoming strike had no visible windup");

            if (capture == "combat-impact")
            {
                if (!Me.Attack(attacker, this) || Horde.Flash[Horde.Count - 1] <= 0f)
                    throw new System.InvalidOperationException("Melee hit did not register");
                TickIncomingAttacks(AttackWindup);
                if (Me.Hp != hp) throw new System.InvalidOperationException("Stagger did not interrupt the attack");
            }
            else if (capture == "combat-hurt")
            {
                Me.Pos = attacker + Vector2.right * 0.95f;
                TickIncomingAttacks(AttackWindup + 0.02f);
                if (Me.Hp != hp) throw new System.InvalidOperationException("Sidestepping the warning arc did not avoid damage");
                Me.Pos = attacker + Vector2.down * 0.86f;
                TickIncomingAttacks(AttackRecovery + 0.02f);
                TickIncomingAttacks(AttackWindup + 0.02f);
                if (Me.Hp >= hp || _damageCue <= 0f)
                    throw new System.InvalidOperationException("A landed attack did not display its damage cue");
            }
            Debug.Log($"[combat] {capture} verified");
        }

        (List<Vector3> warnings, Vector3 direction, float flash) CombatHud()
        {
            _attackWarnings.Clear();
            foreach (var pair in _incoming)
            {
                if (!pair.Value.winding) continue;
                int i = Horde.Id.IndexOf(pair.Key);
                if (i >= 0 && Horde.Flash[i] <= 0) _attackWarnings.Add(_cam.WorldToScreenPoint(Fx.V3(Horde.Pos[i], 1.85f)) + Vector3.up * 32f);
            }
            Vector2 direction = Me.Pos + (_lastDamageSource - Me.Pos).normalized * 1.9f;
            return (_attackWarnings, _cam.WorldToScreenPoint(Fx.V3(direction, 1.1f)), _damageCue / 0.55f);
        }
    }
}
