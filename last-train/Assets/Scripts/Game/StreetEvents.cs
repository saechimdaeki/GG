using UnityEngine;

namespace LastTrain
{
    public partial class Game
    {
        bool _beaconReserved, _blackout, _blackoutOccurred;
        float _beaconTime;

        bool BeaconActive => Phase == "night" && _beaconTime > 0 && _drop != null;

        void StartStreetNight()
        {
            EndFlare();
            if (!_beaconReserved) return;
            _beaconTime = T.streetEvents.beaconSeconds;
            LightBeacon(new Color(0.28f, 0.72f, 1f));
            Say("유인등이 켜졌다. 골목 쪽으로 좀비가 움직인다");
        }

        void ResetStreetEvents()
        {
            EndFlare();
            if (_drop != null)
            {
                if (_searching == _drop) _searching = null;
                if (_lootSource == _drop) CloseLoot();
                City.Containers.Remove(_drop);
                Destroy(_drop.t.gameObject);
                _drop = null;
            }
            _beaconReserved = _blackout = _blackoutOccurred = false;
            _beaconTime = 0;
            City.SetShelterPower(true);
        }

        void TickStreetEvents(float dt)
        {
            if (Phase != "night") return;
            if (_beaconTime > 0)
            {
                _beaconTime -= dt;
                if (_beaconTime <= 0)
                {
                    EndFlare();
                    Say("유인등이 꺼졌다. 다시 쉼터로 몰려온다");
                }
            }
            var cfg = T.streetEvents;
            if (_blackoutOccurred || Day != cfg.blackoutDay || PhaseT > T.clock.nightSeconds - cfg.blackoutAfter) return;
            _blackout = _blackoutOccurred = true;
            City.SetShelterPower(false);
            const string alert = "쉼터 정전 — 실내 배전함에서 고철로 복구할 수 있다";
            if (Sleeping) Wake(alert);
            else Say(alert);
        }

        bool TryReserveBeacon()
        {
            if (Phase != "day" || _drop == null || _beaconReserved || _drop.searched || _drop.loot != null ||
                _searching != null || Vector2.Distance(_drop.pos, Me.Pos) >= 1.9f) return false;
            _beaconReserved = true;
            City.Containers.Remove(_drop);
            EndFlare();
            Say("유인등을 예약했다. 해가 지면 여기서 좀비를 끌어들인다");
            return true;
        }

        bool InteractShelterPower(bool pressed)
        {
            if (!_blackout || Vector2.Distance(Me.Pos, City.ShelterBreaker) >= 1.65f) return false;
            int cost = T.streetEvents.blackoutScrap;
            _prompt = Me.Bag["scrap"] >= cost ? $"E · 쉼터 전력 복구 (고철 {cost})" : $"쉼터 배전함 · 고철 {cost} 필요";
            if (pressed && Me.Bag["scrap"] >= cost)
            {
                Me.Bag["scrap"] -= cost;
                _blackout = false;
                City.SetShelterPower(true);
                Say("전력이 돌아왔다");
            }
            return true;
        }

        bool TryDivert(int zombie, Vector2 survivor)
        {
            if (!BeaconActive || (Horde.Pos[zombie] - survivor).sqrMagnitude < 16f) return false;
            float radius = T.streetEvents.beaconRadius;
            if ((Horde.Pos[zombie] - _drop.pos).sqrMagnitude > radius * radius) return false;
            Horde.Alert(zombie, _drop.pos, 6f);
            return true;
        }

        string StreetEventStatus()
        {
            if (_blackout)
                return $"<color=#ffb77a>쉼터 정전</color>  ·  배전함에 고철 {T.streetEvents.blackoutScrap}";
            if (BeaconActive)
                return $"<color=#9bdfff>유인등 가동</color>  ·  {_beaconTime:0}초 뒤 소등";
            if (_beaconReserved && Phase == "day")
                return "<color=#9bdfff>유인등 예약</color>  ·  해가 지면 자동 점등";
            if (_flareLight && _drop != null)
                return $"<color=#ffb77a>긴급 보급</color>  ·  {Vector2.Distance(_drop.pos, Me.Pos):0}m";
            return "";
        }
    }
}
