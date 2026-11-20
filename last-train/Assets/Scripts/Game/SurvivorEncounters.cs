using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    public partial class Game
    {
        enum EncounterKind { None, Request, Rescue, Lure }
        EncounterKind _scheduledEncounter, _encounterKind;
        float _encounterAt, _rescueTime, _rescueHealth;
        string _requestItem;
        float _ambushTime;
        bool _ambushCommitted;
        Vector2 _ambushDirection;
        bool _storyEncounter, _helpedMechanic, _rescuedRelay;
        Vector2 _storyEncounterAt;
        Vector2 _encounterPosition;
        GameObject _encounterNpc;
        readonly HashSet<int> _attackers = new();

        void ScheduleEncounter()
        {
            ClearEncounter();
            var cfg = T.encounters;
            if (Day is 2 or 3)
            {
                _storyEncounter = true;
                _scheduledEncounter = Day == 2 ? EncounterKind.Request : EncounterKind.Rescue;
                _encounterAt = T.clock.daySeconds - (Day == 2 ? 18f : 32f);
                _storyEncounterAt = Day == 2 ? City.MarketSquare + Vector2.down * 3.5f
                    : City.StationCourt + Vector2.left * 3.5f;
                return;
            }
            // A separate seeded roll keeps encounter frequency independent of looting and combat.
            var random = new System.Random(_encounterSeed ^ (Day * 104729));
            if (Day == cfg.requestDay && random.NextDouble() < cfg.requestChance)
            {
                _scheduledEncounter = EncounterKind.Request;
                _encounterAt = T.clock.daySeconds - cfg.requestAt;
            }
            else if (Day == cfg.rescueDay && random.NextDouble() < cfg.rescueChance)
            {
                _scheduledEncounter = EncounterKind.Rescue;
                _encounterAt = T.clock.daySeconds - cfg.rescueAt;
            }
            else if (random.NextDouble() < cfg.ambushChance)
            {
                _scheduledEncounter = EncounterKind.Lure;
                _encounterAt = T.clock.daySeconds - cfg.ambushAt;
            }
        }

        void TickEncounter(float dt)
        {
            if (Phase != "day" || OnMarketUpper) return;
            if (_scheduledEncounter != EncounterKind.None && PhaseT <= _encounterAt)
            {
                var kind = _scheduledEncounter;
                if (_storyEncounter && kind == EncounterKind.Rescue &&
                    (Me.Pos - _storyEncounterAt).sqrMagnitude > 12f * 12f) return;
                _scheduledEncounter = EncounterKind.None;
                if (_storyEncounter)
                {
                    StartEncounter(kind, _storyEncounterAt);
                    if (_encounterKind == EncounterKind.None) _scheduledEncounter = kind;
                    return;
                }
                var cfg = T.encounters;
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    float angle = (float)_rng.NextDouble() * Mathf.PI * 2;
                    var distance = Mathf.Lerp(cfg.minDistance, cfg.maxDistance, (float)_rng.NextDouble());
                    var point = Me.Pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (!Placeable(point) || InWall(point, 1.15f, City.Walls) ||
                        Vector2.Distance(point, City.Station) < 8f) continue;
                    StartEncounter(kind, point);
                    break;
                }
            }
            if (_encounterKind == EncounterKind.Lure && _ambushCommitted)
            {
                _ambushTime -= dt;
                if (_ambushTime <= 0) ResolveAmbush();
            }
            if (_encounterKind != EncounterKind.Rescue || _attackers.Count == 0) return;
            _rescueTime -= dt;
            _rescueHealth -= dt * Horde.ContactDps(_encounterPosition, 0.6f);
            if (_rescueTime > 0 && _rescueHealth > 0) return;
            Say("구조에 실패했다. 골목에서 더 이상 응답이 없다");
            ClearEncounter();
        }

        void StartEncounter(EncounterKind kind, Vector2 position)
        {
            if (kind == EncounterKind.Rescue)
            {
                for (int n = 0; n < T.encounters.attackers && Horde.Count < T.spawn.maxAlive; n++)
                {
                    int? kindOverride = _storyEncounter ? (n == 0 ? Horde.Screamer : 1) : null;
                    int i = SpawnOne(position, 2.8f, 5.5f, kindOverride);
                    if (i < 0) continue;
                    _attackers.Add(Horde.Id[i]);
                    Horde.Alert(i, position, T.encounters.rescueWindow);
                }
                if (_attackers.Count == 0) return;
            }

            _encounterKind = kind;
            _encounterPosition = position;
            _encounterNpc = _actors.SpawnNpc(kind == EncounterKind.Request ? "Worker_Female" : "Casual_Male", position, 180, kind == EncounterKind.Lure ? "lure" : kind == EncounterKind.Rescue ? "station" : "");
            _requestItem = _storyEncounter ? "bandage" : (_encounterSeed & 1) == 0 ? "food" : "bandage";
            _rescueTime = T.encounters.rescueWindow;
            _rescueHealth = T.player.hp;
            if (_storyEncounter)
                Say(kind == EncounterKind.Request ? "시장 수리공 윤서가 다쳤다. 붕대가 필요하다."
                    : "역무원 지호: 이쪽입니다! 저 비명 때문에 놈들이 몰려들고 있어요!");
            else if (kind == EncounterKind.Lure)
                Say("낯선 생존자: 이리 와요. 철문 뒤에 물자가 있어요.");
            else Say(kind == EncounterKind.Request ? "골목에서 누군가 손을 흔든다" : "살려 달라는 소리다! 근처에 좀비가 몰리고 있다");
        }

        void DirectEncounterAttackers()
        {
            if (_attackers.Count == 0) return;
            for (int i = 0; i < Horde.Count; i++)
            {
                if (!_attackers.Contains(Horde.Id[i]) || (Horde.Pos[i] - Me.Pos).sqrMagnitude < 2.8f * 2.8f) continue;
                Horde.Alert(i, _encounterPosition, 2f);
            }
        }

        void EncounterAttackerDefeated(int id)
        {
            if (_attackers.Remove(id) && _attackers.Count == 0)
                Say("주변 좀비를 처리했다. 생존자에게 말을 걸자");
        }

        bool InteractEncounter(bool pressed)
        {
            if (_encounterKind == EncounterKind.None || _ambushCommitted ||
                Vector2.Distance(Me.Pos, _encounterPosition) >= 1.9f ||
                !Clear(Me.Pos, _encounterPosition)) return false;
            if (_encounterKind == EncounterKind.Lure)
            {
                _prompt = "E · 물자 제안 듣기  /  손에 쇠파이프를 숨기고 있다";
                if (pressed) BeginAmbush();
                return true;
            }
            if (_encounterKind == EncounterKind.Rescue && _attackers.Count > 0)
            {
                _prompt = $"생존자를 공격하는 좀비 {_attackers.Count}마리";
                return true;
            }
            if (_encounterKind == EncounterKind.Request)
            {
                string ask = Survivor.Name(_requestItem);
                bool have = Me.Bag[_requestItem] > 0;
                _prompt = have ? $"E · {ask} 1개 건네기" : $"생존자 · {ask} 1개가 필요하다";
                if (!pressed || !have) return true;
                Me.Bag[_requestItem]--;
                _peopleHelped++;
                if (_storyEncounter)
                {
                    _helpedMechanic = true;
                    GiveEncounterReward("scrap", 2);
                    RecordClue(EscapeRoute.Road, announce: false);
                    Say("윤서: 운송차 정비 기록과 고철이에요. 검문소 차량을 살리는 데 보태세요.");
                }
                else
                {
                    GiveEncounterReward(Powered ? "scrap" : "fuse", 1);
                    Say(Powered ? "“정말 고마워요. 이 고철은 가져가세요.”" : "“정말 고마워요. 역 배전반에 맞는 퓨즈예요.”");
                }
            }
            else
            {
                _prompt = "E · 구조한 생존자에게 말 걸기";
                if (!pressed) return true;
                _rescuedSomeone = true;
                GiveEncounterReward("food", 2);
                GiveEncounterReward(Powered ? "bandage" : "fuse", 1, 0.55f);
                if (_storyEncounter)
                {
                    _rescuedRelay = true;
                    RecordClue(EscapeRoute.Rail, announce: false);
                    Say(Powered ? "지호: 덕분에 살았습니다. 박 기관사에게 합류할게요."
                        : "지호: 막차 운행표와 퓨즈예요. 박 기관사에게 제가 살아 있다고 전해 주세요.");
                }
                else Say("“덕분에 살았어요. 제가 챙겨 둔 물자예요.”");
            }
            if (_encounterNpc) Destroy(_encounterNpc, 3.5f);
            _encounterNpc = null;
            ClearEncounter();
            return true;
        }

        void BeginAmbush()
        {
            _ambushCommitted = true;
            _ambushTime = T.encounters.ambushWindup;
            _ambushDirection = (Me.Pos - _encounterPosition).normalized;
            if (_ambushDirection.sqrMagnitude < 0.001f) _ambushDirection = Vector2.down;
            if (_encounterNpc) _encounterNpc.transform.rotation = Quaternion.LookRotation(Fx.V3(_ambushDirection));
            _fx.Fan(_encounterPosition, _ambushDirection, T.encounters.ambushReach, 43f * Mathf.Deg2Rad,
                new Color(1f, 0.26f, 0.19f, 0.9f), _ambushTime);
            Noise(_encounterPosition, 15f);
            Say("낯선 생존자가 무기를 꺼냈다! 옆으로 피하자.");
        }

        void ResolveAmbush()
        {
            Vector2 offset = Me.Pos - _encounterPosition;
            if (offset.sqrMagnitude <= T.encounters.ambushReach * T.encounters.ambushReach &&
                Vector2.Angle(_ambushDirection, offset) <= 43f && Clear(Me.Pos, _encounterPosition))
                HurtPlayer(Me, T.encounters.ambushDamage, _encounterPosition);
            Say("낯선 생존자가 달아났다. 소리를 듣고 좀비가 모인다.");
            ClearEncounter();
        }

        void VerifyAmbush()
        {
            ClearEncounter();
            Horde = new Horde(T.zombies, City.ZombieWalls);
            var at = City.MarketSquare;
            float health = Me.Hp;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Me.Pos = at + Vector2.down * 1.25f;
                StartEncounter(EncounterKind.Lure, at);
                if (!InteractEncounter(true) || !_ambushCommitted || Me.Hp != health)
                    throw new System.InvalidOperationException("Ambush must give a warning before dealing damage");
                if (attempt == 0) Me.Pos = at + Vector2.right * 1.4f;
                TickEncounter(T.encounters.ambushWindup + 0.01f);
                float expected = attempt == 0 ? health : health - T.encounters.ambushDamage;
                if (!Mathf.Approximately(Me.Hp, expected) || _encounterKind != EncounterKind.None)
                    throw new System.InvalidOperationException("Ambush hit or dodge failed");
            }
            Debug.Log("[ambush] warning, evasion, damage and event cleanup verified");
        }

        void GiveEncounterReward(string item, int count, float labelOffset = 0f)
        {
            Me.Take(item, count, out int acquired);
            if (acquired > 0) PopText(_encounterPosition + Vector2.up * labelOffset, $"{Survivor.Name(item)} ×{acquired}", new Color(0.66f, 0.95f, 0.74f));
            if (acquired < count) LeaveSupplies(_encounterPosition, (item, count - acquired));
        }

        void ClearEncounter()
        {
            if (_encounterNpc) Destroy(_encounterNpc);
            _encounterNpc = null;
            _encounterKind = _scheduledEncounter = EncounterKind.None;
            _storyEncounter = _ambushCommitted = false;
            _attackers.Clear();
        }

        (Vector3 screen, string label, bool visible) EncounterHud()
        {
            if (_encounterKind == EncounterKind.None)
            {
                if (!_storyEncounter || PhaseT > _encounterAt || _scheduledEncounter != EncounterKind.Rescue) return default;
                float pendingDistance = Vector2.Distance(Me.Pos, _storyEncounterAt);
                return (_cam.WorldToScreenPoint(Fx.V3(_storyEncounterAt, 1.75f)), $"정비 광장 구조 신호  {pendingDistance:0}m", true);
            }
            float distance = Vector2.Distance(Me.Pos, _encounterPosition);
            string label = _encounterKind == EncounterKind.Lure
                ? (_ambushCommitted ? "매복 공격" : $"낯선 생존자  {distance:0}m")
                : _encounterKind == EncounterKind.Request ? $"물자 요청  {distance:0}m"
                : _attackers.Count > 0 ? $"구조 요청  {distance:0}m · {Mathf.CeilToInt(_rescueTime)}초"
                : $"구조된 생존자  {distance:0}m";
            if (_storyEncounter) label = (_encounterKind == EncounterKind.Request ? "윤서 · " : "지호 · ") + label;
            return (_cam.WorldToScreenPoint(Fx.V3(_encounterPosition, 1.75f)), label, true);
        }

        void VerifyStoryEncounters()
        {
            Horde = new Horde(T.zombies, City.ZombieWalls);
            Day = 2;
            StartPhase("day");
            PhaseT = _encounterAt;
            TickEncounter(0f);
            Me.Pos = _encounterPosition;
            InteractEncounter(true);
            if (_encounterKind != EncounterKind.Request || _helpedMechanic)
                throw new System.InvalidOperationException("Mechanic should wait for a bandage");
            Me.Take("bandage", 1);
            InteractEncounter(true);
            if (!_helpedMechanic || !_routeClues[(int)EscapeRoute.Road] || Me.Bag["scrap"] != 2 || Me.Bag["bandage"] != 0)
                throw new System.InvalidOperationException("Mechanic exchange failed");

            Day = 3;
            StartPhase("day");
            PhaseT = _encounterAt;
            Me.Pos = City.Start;
            TickEncounter(0f);
            if (_encounterKind != EncounterKind.None || !EncounterHud().visible)
                throw new System.InvalidOperationException("Rescue should show a signal before the approach");
            Me.Pos = _storyEncounterAt;
            TickEncounter(0f);
            if (_encounterKind != EncounterKind.Rescue || _attackers.Count != 2 || !Horde.Kind.Contains(Horde.Screamer))
                throw new System.InvalidOperationException("Story rescue failed to start");
            InteractEncounter(true);
            if (_rescuedRelay) throw new System.InvalidOperationException("Rescue should wait for the attackers");
            foreach (int id in new List<int>(_attackers))
            {
                Horde.Despawn(id);
                EncounterAttackerDefeated(id);
            }
            InteractEncounter(true);
            if (!_rescuedRelay || !_routeClues[(int)EscapeRoute.Rail] || Me.Bag["fuse"] != 1 || !EndingStory().Contains("지호"))
                throw new System.InvalidOperationException("Rescue reward or ending dialogue missing");

            Me.Pos = City.MarketSquare;
            Horde.Spawn(Me.Pos + Vector2.up, Horde.Armored);
            Horde.Spawn(Me.Pos + Vector2.right * 4f, Horde.Screamer);
            Horde.Spawn(Me.Pos + Vector2.right * 7f, 0);
            Horde.Step(0f, new[] { Me.Pos });
            Horde.Heading[0] = Vector2.down;
            float hp = Horde.Hp[0];
            Me.Attack(Me.Pos + Vector2.up, this);
            float frontDamage = hp - Horde.Hp[0];
            Horde.Heading[0] = Vector2.up;
            Me.TickCooldown(2f);
            hp = Horde.Hp[0];
            Me.Attack(Me.Pos + Vector2.up, this);
            if (frontDamage <= 0f || hp - Horde.Hp[0] <= frontDamage * 2f)
                throw new System.InvalidOperationException("Armored zombie must take more damage from behind");
            Horde.Alert(1, Me.Pos, 4f);
            TickScreamers();
            if (!Horde.IsAware(2) || !_screamed.Contains(Horde.Id[1]))
                throw new System.InvalidOperationException("Screamer did not alert the nearby zombie");
            Debug.Log("[story] mechanic exchange, relay rescue, ending, armored flank and scream verified");
        }

        string TradeWarning(Tuning.TradeCfg trade)
        {
            string reason = Me.TradeBlockReason(trade);
            return reason.Length > 0 ? $"  <color=#ff7060>({reason})</color>" : "";
        }
    }
}
