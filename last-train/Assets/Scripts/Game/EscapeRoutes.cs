using System.Collections.Generic;
using UnityEngine;

namespace LastTrain
{
    internal enum EscapeRoute { Rail, Road, Drain }

    public partial class Game
    {
        readonly bool[] _contacts = new bool[3], _routeClues = new bool[3];
        readonly Vector2[] _cluePositions = new Vector2[3];
        readonly GameObject[] _clueProps = new GameObject[3];
        EscapeRoute _trackedRoute;
        EscapeRoute? _escapeRoute;
        bool _roadRepaired, _evacAtNight;
        string _returnPhase;
        float _returnClock;
        int _drainValves;

        static string RouteName(EscapeRoute route) => route switch
        {
            EscapeRoute.Road => "서쪽 검문소",
            EscapeRoute.Drain => "남부 배수로",
            _ => "세운역 열차",
        };

        static Vector2 RoutePosition(EscapeRoute route) => route switch
        {
            EscapeRoute.Road => City.WestGate,
            EscapeRoute.Drain => City.DrainExit,
            _ => City.Station,
        };

        static Vector2 ContactPosition(EscapeRoute route) => route switch
        {
            EscapeRoute.Road => City.MarketContact,
            EscapeRoute.Drain => City.DrainContact,
            _ => City.Trader + Vector2.down * 2,
        };

        static string ContactName(EscapeRoute route) => route switch
        {
            EscapeRoute.Road => "시장 연락책",
            EscapeRoute.Drain => "시설반 연락책",
            _ => "박 기관사",
        };

        void SpawnRouteContacts()
        {
            _actors.SpawnNpc("Worker_Female", City.MarketContact, 180);
            _actors.SpawnNpc("Worker_Male", City.DrainContact, 180);
        }

        static string ClueName(EscapeRoute route) => route switch
        {
            EscapeRoute.Road => "운송차 정비 기록",
            EscapeRoute.Drain => "수문 배관 도면",
            _ => "막차 운행표",
        };

        void CreateRouteClues()
        {
            var anchors = new[] { City.StationCourt + Vector2.down * 3.5f,
                City.MarketSquare + Vector2.left * 4.5f, City.DrainContact + Vector2.right * 3.5f };
            for (int i = 0; i < anchors.Length; i++)
            {
                var at = anchors[i];
                var clear = at;
                for (int offset = 1; offset <= 8 && (!Placeable(clear) || InWall(clear, 0.65f, City.Walls)); offset++)
                {
                    float angle = offset * 2.4f;
                    clear = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (1f + offset * 0.55f);
                }
                _cluePositions[i] = clear;
                var file = Visual(PrimitiveType.Cube, new Color(0.23f, 0.71f, 0.72f), 0.2f, new Vector3(0.52f, 0.14f, 0.37f));
                file.name = ClueName((EscapeRoute)i);
                file.position = Fx.V3(clear, 0.085f);
                _clueProps[i] = file.gameObject;
            }
        }

        void RecordClue(EscapeRoute route, bool announce = true)
        {
            int i = (int)route;
            if (_routeClues[i]) return;
            _routeClues[i] = true;
            if (_clueProps[i]) _clueProps[i].SetActive(false);
            if (announce) Say(route switch
            {
                EscapeRoute.Road => "운송차 정비 기록 확보. 서쪽 검문소 차량의 시동 장치를 고치면 도로로 빠져나갈 수 있다.",
                EscapeRoute.Drain => "수문 배관 도면 확보. 남부 배수로의 밸브 두 개를 열면 봉쇄선 아래로 이어진다.",
                _ => "막차 운행표 확보. 세운역에 전력이 돌아오면 박 기관사가 출발할 수 있다.",
            });
        }

        bool InteractRouteClue(bool pressed)
        {
            for (int i = 0; i < _cluePositions.Length; i++)
            {
                if (_routeClues[i] || (Me.Pos - _cluePositions[i]).sqrMagnitude >= 1.4f * 1.4f) continue;
                var route = (EscapeRoute)i;
                _prompt = $"E · {ClueName(route)} 기록";
                if (pressed) RecordClue(route);
                return true;
            }
            return false;
        }

        bool HasDrainTool() => Me.Slots.Exists(slot => slot.id == "pipe" || slot.id == "axe");

        bool ValveOpen(int index) => (_drainValves & (1 << index)) != 0;
        bool DrainReady => ValveOpen(0) && ValveOpen(1);

        bool InteractDrainValve(bool pressed)
        {
            for (int i = 0; i < City.DrainValves.Length; i++)
            {
                if (Vector2.Distance(Me.Pos, City.DrainValves[i]) >= 1.4f) continue;
                if (!_contacts[(int)EscapeRoute.Drain])
                {
                    _prompt = "수문 밸브 · 시설반과 먼저 연락";
                    return true;
                }
                if (ValveOpen(i)) continue;
                bool hasTool = HasDrainTool();
                _prompt = hasTool ? $"E · 수문 밸브 {i + 1} 열기" : "수문 밸브 · 쇠파이프 또는 소방도끼 필요";
                if (pressed && hasTool)
                {
                    _drainValves |= 1 << i;
                    City.SetValveIndicator(i, true);
                    Say(DrainReady ? "두 밸브를 열었다. 수문으로 진입할 수 있다" : "밸브를 열었다. 반대편 밸브도 확인하자");
                }
                return true;
            }
            return false;
        }

        string RouteRequirement(EscapeRoute route, bool detailed = false)
        {
            if (!_contacts[(int)route]) return detailed ? $"{ContactName(route)}와 먼저 연락 필요" : ContactName(route);
            if (!_routeClues[(int)route]) return $"{ClueName(route)} 찾기";
            return route switch
            {
                EscapeRoute.Rail when !Powered => $"역 전력 {FusesIn}/{T.station.fusesNeeded}",
                EscapeRoute.Road when !_roadRepaired && Me.Bag["scrap"] < T.escape.roadScrap => $"고철 {Me.Bag["scrap"]}/{T.escape.roadScrap}",
                EscapeRoute.Drain when !DrainReady && !HasDrainTool() => "쇠파이프 / 도끼 필요",
                EscapeRoute.Drain when !DrainReady => $"수문 밸브 {(ValveOpen(0) || ValveOpen(1) ? 1 : 0)}/2",
                _ => "",
            };
        }

        bool InteractEscapeRoutes(bool pressed)
        {
            if (InPractice || Phase == "evac") return false;
            if (InteractRouteClue(pressed)) return true;
            if (InteractDrainValve(pressed)) return true;
            for (int i = 0; i < _contacts.Length; i++)
            {
                var route = (EscapeRoute)i;
                if (Vector2.Distance(Me.Pos, ContactPosition(route)) < 1.7f && !_contacts[i])
                {
                    _prompt = $"E · {ContactName(route)}와 연락";
                    if (pressed) DiscoverRoute(route);
                    return true;
                }
                if (Vector2.Distance(Me.Pos, RoutePosition(route)) >= 1.65f) continue;
                string requirement = RouteRequirement(route, true);
                _prompt = requirement.Length == 0 ? $"E · {RouteName(route)} 탈출 시도" : $"{RouteName(route)} · {requirement}";
                if (pressed && requirement.Length == 0) TryBeginEscape(route);
                return true;
            }
            return false;
        }

        void DiscoverRoute(EscapeRoute route)
        {
            _contacts[(int)route] = true;
            _trackedRoute = route;
            Say(route switch
            {
                EscapeRoute.Road => $"시장 연락책 · 장터에 운송차 정비 기록을 뒀어요. 시동을 살리려면 고철 {T.escape.roadScrap}개가 필요해요.",
                EscapeRoute.Drain => "시설반 · 근처에서 배관 도면을 찾아요. 쇠파이프나 소방도끼로 밸브 두 개를 열어야 해요.",
                _ => $"박 기관사 · 정비 광장에서 막차 운행표를 찾아요. 배전반에 퓨즈 {T.station.fusesNeeded}개도 필요합니다.",
            });
        }

        bool TryBeginEscape(EscapeRoute route)
        {
            if (Phase != "day" && Phase != "night" || RouteRequirement(route).Length > 0) return false;
            if (route == EscapeRoute.Road && !_roadRepaired)
            {
                Me.Bag["scrap"] -= T.escape.roadScrap;
                _roadRepaired = true;
            }
            PrepareEscape(route);
            return true;
        }

        void PrepareEscape(EscapeRoute route)
        {
            _returnPhase = Phase;
            _returnClock = PhaseT;
            _evacAtNight = IsNight;
            _escapeRoute = route;
            _trackedRoute = route;
            StartPhase("evac");
            Noise(RoutePosition(route), route == EscapeRoute.Drain ? 10f : 24f);
        }

        public float EvacNeed => _escapeRoute switch
        {
            EscapeRoute.Road => T.escape.roadSeconds,
            EscapeRoute.Drain => T.escape.drainSeconds,
            _ => T.station.poweredEvacSeconds,
        };

        float EvacRadius => _escapeRoute == EscapeRoute.Drain ? T.escape.drainRadius : T.clock.evacRadius;
        float EvacSpawnMultiplier => _escapeRoute switch
        {
            EscapeRoute.Road => T.escape.roadSpawn,
            EscapeRoute.Drain => T.escape.drainSpawn,
            _ => T.station.poweredSpawnMult,
        };

        void TickEscape(float dt)
        {
            if (!_escapeRoute.HasValue) return;
            Vector2 exit = RoutePosition(_escapeRoute.Value);
            if (Vector2.Distance(Me.Pos, exit) > T.escape.retreatDistance)
            {
                _escapeRoute = null;
                Phase = _returnPhase;
                PhaseT = _returnClock;
                _nightCallT = 0;
                Say("탈출 시도를 중단했다. 준비된 통로에서 다시 시도할 수 있다");
                return;
            }
            foreach (var player in Players)
            {
                if (!player.Alive || Vector2.Distance(player.Pos, exit) > EvacRadius) continue;
                PhaseT += dt;
                if (PhaseT >= EvacNeed) StartPhase("won");
                return;
            }
        }

        internal void VerifyEscapeRoutes()
        {
            Phase = "day";
            PhaseT = 83f;
            foreach (EscapeRoute route in System.Enum.GetValues(typeof(EscapeRoute)))
            {
                Me.Pos = RoutePosition(route);
                if (TryBeginEscape(route)) throw new System.InvalidOperationException("Route opened without its contact");
                DiscoverRoute(route);
                if (TryBeginEscape(route)) throw new System.InvalidOperationException("Route opened without its required supplies");
                switch (route)
                {
                    case EscapeRoute.Rail:
                        Me.Take("fuse", T.station.fusesNeeded);
                        InsertFuses();
                        break;
                    case EscapeRoute.Road: Me.Take("scrap", T.escape.roadScrap); break;
                    case EscapeRoute.Drain:
                        Me.Take("pipe", 1);
                        Me.Pos = City.DrainValves[0];
                        InteractDrainValve(true);
                        if (DrainReady) throw new System.InvalidOperationException("One valve opened the drain");
                        Me.Pos = City.DrainValves[1];
                        InteractDrainValve(true);
                        Me.Pos = RoutePosition(route);
                        break;
                }

                if (TryBeginEscape(route)) throw new System.InvalidOperationException("Route opened without its clue");
                Me.Pos = _cluePositions[(int)route];
                if (!InteractRouteClue(true) || !_routeClues[(int)route])
                    throw new System.InvalidOperationException("Route clue interaction failed");
                Me.Pos = RoutePosition(route);
                if (!TryBeginEscape(route) || IsNight) throw new System.InvalidOperationException("Daytime escape could not start");
                Me.Pos = RoutePosition(route) + Vector2.down * (EvacRadius + 1f);
                TickEscape(1f);
                if (PhaseT != 0f) throw new System.InvalidOperationException("Progress advanced outside the escape zone");
                Me.Pos = RoutePosition(route) + Vector2.down * (T.escape.retreatDistance + 1f);
                TickEscape(1f);
                if (Phase != "day" || PhaseT != 83f) throw new System.InvalidOperationException("Escape retreat did not restore the day");
                Me.Pos = RoutePosition(route);
                if (!TryBeginEscape(route)) throw new System.InvalidOperationException("Could not retry a prepared route");
                TickEscape(EvacNeed);
                if (Phase != "won") throw new System.InvalidOperationException("Route could not lead to victory");
                Phase = "day";
                PhaseT = 83f;
                _escapeRoute = null;
            }
            Debug.Log("[routes] contacts, clues, preparation, retreat and victory verified for rail, road and drain");
        }

        string RouteObjectives()
        {
            if (_escapeRoute.HasValue) return $"{RouteName(_escapeRoute.Value)} · 탈출 준비 중";
            var lines = new List<string>(3);
            for (int i = 0; i < _contacts.Length; i++)
            {
                var route = (EscapeRoute)i;
                string requirement = RouteRequirement(route);
                lines.Add($"{(route == _trackedRoute ? "› " : "")}{RouteName(route)} · {(requirement.Length == 0 ? "탈출 가능" : requirement)}");
            }
            return string.Join("\n", lines);
        }

        (Vector3 screen, string label, bool active) RouteMarker()
        {
            var route = _escapeRoute ?? _trackedRoute;
            bool known = _escapeRoute.HasValue || _contacts[(int)route];
            Vector2 at = known ? RoutePosition(route) : ContactPosition(route);
            string name = known ? RouteName(route) : ContactName(route);
            if (!_escapeRoute.HasValue && known)
            {
                if (!_routeClues[(int)route])
                {
                    at = _cluePositions[(int)route];
                    name = ClueName(route);
                }
                else if (route == EscapeRoute.Rail && !Powered)
                {
                    at = City.Panel;
                    name = "역 배전반";
                }
                else if (route == EscapeRoute.Drain && !DrainReady && HasDrainTool())
                {
                    int i = ValveOpen(0) ? 1 : 0;
                    at = City.DrainValves[i];
                    name = $"수문 밸브 {i + 1}";
                }
            }
            string label = $"{name} {Vector2.Distance(Me.Pos, at):0}m";
            return (_cam.WorldToScreenPoint(Fx.V3(at, 1.5f)), label, _escapeRoute.HasValue);
        }
    }
}
