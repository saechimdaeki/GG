using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastTrain
{
    public partial class Game
    {
        bool _bagOpen;
        int _bagRow;

        void HandleBag(Keyboard keys)
        {
            _moving = Me.Sprinting = Me.Crouching = false;
            _searching = null;
            _prompt = "";
            if (keys == null) return;
            int rows = Survivor.SupplyItems.Length;
            if (keys.upArrowKey.wasPressedThisFrame) _bagRow = (_bagRow + rows - 1) % rows;
            if (keys.downArrowKey.wasPressedThisFrame) _bagRow = (_bagRow + 1) % rows;
            if (!keys.xKey.wasPressedThisFrame) return;
            string item = Survivor.SupplyItems[_bagRow];
            int have = Me.Bag[item];
            if (have == 0) return;
            int quantity = keys.shiftKey.isPressed ? have : Mathf.Min(have, item == "nails" ? 10 : 1);
            Me.Bag[item] -= quantity;
            LeaveSupplies(Me.Pos, (item, quantity));
        }

        City.Container LeaveSupplies(Vector2 at, (string item, int count) goods)
        {
            foreach (var parcel in City.Containers)
            {
                if (!parcel.parcel || (parcel.pos - at).sqrMagnitude > 0.55f * 0.55f) continue;
                AddLoot(parcel.loot, goods.item, goods.count);
                return parcel;
            }
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "Supply parcel";
            box.transform.position = Fx.V3(at, 0.17f);
            box.transform.localScale = new Vector3(0.62f, 0.34f, 0.47f);
            City.Paint(box, new Color(0.43f, 0.47f, 0.29f));
            var container = new City.Container
            {
                pos = at, t = box.transform, basePos = box.transform.position, kind = "물자 꾸러미",
                loot = new List<(string, int)> { goods }, parcel = true,
            };
            City.Containers.Add(container);
            return container;
        }
    }
}
