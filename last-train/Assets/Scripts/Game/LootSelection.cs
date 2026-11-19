using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastTrain
{
    public partial class Game
    {
        City.Container _lootSource;
        int _lootRow;
        bool _blockAttackUntilRelease;

        void OpenLoot(City.Container container)
        {
            if (container.loot.Count == 0)
            {
                EmptyContainer(container);
                return;
            }
            _lootSource = container;
            _lootRow = 0;
            _prompt = "";
            _blockAttackUntilRelease = true;
        }

        void HandleLoot(Keyboard keys)
        {
            _moving = Me.Sprinting = Me.Crouching = false;
            if (!Me.Alive || !ContainerAccessible(_lootSource) || Vector2.Distance(Me.Pos, _lootSource.pos) > 2.25f)
            {
                CloseLoot();
                return;
            }
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame)
            {
                CloseLoot();
                return;
            }
            int count = _lootSource.loot.Count;
            if (keys.upArrowKey.wasPressedThisFrame) _lootRow = (_lootRow + count - 1) % count;
            if (keys.downArrowKey.wasPressedThisFrame) _lootRow = (_lootRow + 1) % count;
            if (keys.enterKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame) ChooseLoot(_lootRow);
            else if (keys.fKey.wasPressedThisFrame || _args.ContainsKey("autoloot")) TakeAllLoot();
        }

        void ChooseLoot(int row)
        {
            if (_lootSource == null || row < 0 || row >= _lootSource.loot.Count) return;
            _lootRow = row;
            CollectStack(row);
            RefreshLootSource();
        }

        void TakeAllLoot()
        {
            if (_lootSource == null) return;
            bool skippedWeapon = false;
            for (int i = 0; i < _lootSource.loot.Count;)
            {
                if (Me.WouldReplaceWeapon(_lootSource.loot[i].item))
                {
                    skippedWeapon = true;
                    i++;
                }
                else if (!CollectStack(i, silent: true)) i++;
            }
            if (_lootSource.loot.Count > 0)
                Say(skippedWeapon ? "남은 무기는 직접 골라 교체할 수 있다" : "가방에 들어가는 만큼 챙겼다");
            RefreshLootSource();
        }

        bool CollectStack(int row, bool silent = false)
        {
            var (item, count) = _lootSource.loot[row];
            string result = Me.Take(item, count, out int acquired);
            if (acquired == 0)
            {
                if (!silent) Say(result);
                return false;
            }

            if (_lootSource == _pharmacyCase && item == "fuse") _pharmacyFuseRecovered = true;
            PopText(_lootSource.pos, $"{Survivor.Name(item)} ×{acquired}", new Color(1f, 0.92f, 0.6f));
            if (T.IsWeapon(item) && item != "fireworks") Say(result);
            else if (acquired != count && !silent) Say("가방에 들어가는 만큼 챙겼다");
            if (acquired == count)
            {
                _lootSource.loot.RemoveAt(row);
                return true;
            }
            _lootSource.loot[row] = (item, count - acquired);
            return false;
        }

        void RefreshLootSource()
        {
            if (_lootSource.loot.Count == 0)
            {
                EmptyContainer(_lootSource);
                CloseLoot();
            }
            else _lootRow = Mathf.Clamp(_lootRow, 0, _lootSource.loot.Count - 1);
        }

        void CloseLoot()
        {
            _lootSource = null;
            _prompt = "";
            _blockAttackUntilRelease = true;
        }

        void EmptyContainer(City.Container container)
        {
            container.searched = true;
            if (container.parcel)
            {
                City.Containers.Remove(container);
                Destroy(container.t.gameObject);
            }
            else City.MarkSearched(container);
        }

        internal static void AddLoot(List<(string item, int count)> loot, string item, int count)
        {
            int index = loot.FindIndex(row => row.item == item);
            if (index < 0) loot.Add((item, count));
            else loot[index] = (item, loot[index].count + count);
        }

        Hud.LootState LootHud()
        {
            if (_lootSource == null || !Me.Alive || Phase == "lost" || Phase == "won") return default;
            var rows = new List<string>(_lootSource.loot.Count);
            foreach (var (item, count) in _lootSource.loot)
            {
                int weight = T.bag.Weight(item);
                var detail = weight == 0
                    ? Me.WouldReplaceWeapon(item) ? "장착 무기 교체" : "무기 슬롯"
                    : $"{weight * count / 10f:0.0} kg";
                rows.Add($"{Survivor.Name(item)}  ×{count}     <color=#a5b9ac>{detail}</color>");
            }
            return new Hud.LootState(_lootSource.kind, rows, _lootRow, Me.BagLoad, Me.BagCapacity);
        }
    }
}
