using UnityEngine;

namespace LastTrain
{
    public partial class Game
    {
        City.Container _pharmacyCase;
        bool _pharmacyOpened, _pharmacyFuseRecovered;

        void CreatePharmacyCase(City.Container cache)
        {
            _pharmacyCase = cache;
            _pharmacyOpened = false;
            AddLoot(cache.loot, "fuse", 1);
        }

        bool PharmacyPrompt(bool quiet, bool loud)
        {
            if (_pharmacyCase == null || _pharmacyOpened || !ContainerAccessible(_pharmacyCase) ||
                (Me.Pos - _pharmacyCase.pos).sqrMagnitude > 1.65f * 1.65f) return false;

            _prompt = Me.Bag["scrap"] > 0
                ? "잠긴 약국 비상함 · E 분해(고철 1) / G 강제 개방"
                : "잠긴 약국 비상함 · 고철 1개로 분해 / G 강제 개방";
            if (loud || (quiet && Me.Bag["scrap"] > 0)) OpenPharmacyCase(loud);
            return true;
        }

        void OpenPharmacyCase(bool loud)
        {
            if (_pharmacyCase == null || _pharmacyOpened) return;
            if (!loud)
            {
                if (Me.Bag["scrap"] <= 0) return;
                Me.Bag["scrap"]--;
            }
            _pharmacyOpened = true;
            if (loud)
            {
                Vector2 alarm = _pharmacyCase.pos;
                Noise(alarm, 22f);
                for (int i = 0; i < 3 && Horde.Count < T.spawn.maxAlive; i++)
                {
                    int added = SpawnOne(City.ClinicCenter + Vector2.down * 4.3f, 2f, 5.5f);
                    if (added >= 0) Horde.Alert(added, alarm, 13f);
                }
                _combatAudio.Play("blast");
                Say("잠금쇠가 부러졌다. 골목까지 소리가 퍼졌다");
            }
            else Say("약사의 메모 · 퓨즈는 세운역의 마지막 신호등에 써 주세요");
            OpenLoot(_pharmacyCase);
        }

        void VerifyPharmacy()
        {
            Day = 2;
            StartPhase("day");
            Me.Pos = _pharmacyCase.pos + Vector2.down;
            if (!PharmacyPrompt(true, false) || _pharmacyOpened) throw new System.InvalidOperationException("Locked case opened without a cost");
            Me.Take("scrap", 1);
            PharmacyPrompt(true, false);
            if (!_pharmacyOpened || Me.Bag["scrap"] != 0 || _lootSource != _pharmacyCase)
                throw new System.InvalidOperationException("Quiet pharmacy opening failed");
            CloseLoot();
            _pharmacyOpened = false;
            PharmacyPrompt(false, true);
            if (!_pharmacyOpened || _lootSource != _pharmacyCase)
                throw new System.InvalidOperationException("Loud pharmacy opening failed");
            ChooseLoot(_pharmacyCase.loot.FindIndex(row => row.item == "fuse"));
            if (!_pharmacyFuseRecovered || Me.Bag["fuse"] != 1)
                throw new System.InvalidOperationException("Pharmacy fuse did not reach the story state");
            Debug.Log("[pharmacy] quiet and loud opening and fuse pickup verified");
        }
    }
}
