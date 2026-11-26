using UnityEngine;

namespace DesktopTenants
{
    /// <summary>A temporary street stall that the player can visit by resting the cursor on it.</summary>
    public sealed class NeighborhoodEvent
    {
        public enum Kind { FleaMarket, PhotoBooth, LanternStand }

        const float Duration = 24f, HoverSeconds = 0.8f, FarewellSeconds = 3f;
        readonly Desk desk;
        readonly SpriteRenderer stall;
        readonly Sprite[] sprites;
        readonly Bubble label;
        Kind kind;
        float remaining, hoverTime;
        bool visited;

        public NeighborhoodEvent(Desk desk)
        {
            this.desk = desk;
            stall = new GameObject("Neighborhood stall").AddComponent<SpriteRenderer>();
            stall.transform.SetParent(desk.transform);
            stall.sortingOrder = 16;
            stall.enabled = false;
            var pivot = new Vector2(0.5f, 0);
            float pixel = desk.Tune.pixel * 2;
            sprites = new[]
            {
                Props.Make(Props.FleaMarket, pixel, pivot),
                Props.Make(Props.PhotoBooth, pixel, pivot),
                Props.Make(Props.LanternStand, pixel, pivot),
            };
            label = Bubble.Create(stall.transform, Vector3.up * 76, 28);
        }

        public bool Start(Kind eventKind)
        {
            if (remaining > 0) return false;
            kind = eventKind;
            hoverTime = 0;
            visited = false;
            remaining = Duration;
            stall.sprite = sprites[(int)kind];
            stall.enabled = true;
            stall.color = Color.white;
            stall.transform.position = Desk.W(Location);
            label.Say(Title + " · 커서를 올려 참여", 0);
            if (kind == Kind.PhotoBooth)
                foreach (var tenant in desk.Tenants)
                    if (!tenant.Visitor && tenant.Carriable && Mathf.Abs(tenant.P.x - Location.x) < 450)
                        tenant.Gather(Location.x, Duration, "사진 찍으러 왔어요!", "heart", 2f, spread: 80);
            return true;
        }

        string Title => kind switch
        {
            Kind.FleaMarket => "동네 벼룩시장",
            Kind.PhotoBooth => "즉석 사진관",
            _ => "소원 등불",
        };

        Vector2 Location => new Vector2(desk.ScreenW * 0.2f, desk.FloorY - 4);

        public void Tick(float dt)
        {
            if (remaining <= 0) return;
            remaining -= dt;
            if (remaining <= 0)
            {
                stall.enabled = false;
                label.Hide();
                return;
            }

            var at = Location;
            if (visited && kind == Kind.LanternStand)
                at.y -= (FarewellSeconds - remaining) * 75f;
            stall.transform.position = Desk.W(at);
            if (visited) return;
            var size = stall.sprite.bounds.size;
            var bounds = new Rect(at.x - size.x / 2, at.y - size.y, size.x, size.y);
            hoverTime = bounds.Contains(desk.Mouse) ? hoverTime + dt : 0f;
            stall.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.58f), hoverTime / HoverSeconds);
            if (hoverTime >= HoverSeconds) Visit(at);
        }

        void Visit(Vector2 at)
        {
            visited = true;
            remaining = FarewellSeconds;
            stall.color = Color.white;
            int reward = desk.Tune.neighborhoodReward;
            switch (kind)
            {
                case Kind.FleaMarket:
                    label.Say("벼룩시장에서 보물을 찾았어요!", 0);
                    desk.SpawnCoinAt(at - new Vector2(0, 54), reward);
                    break;
                case Kind.PhotoBooth:
                    label.Say("하나, 둘, 셋! 단체 사진 완성", 0);
                    foreach (var tenant in desk.Tenants)
                        if (!tenant.Visitor && tenant.Carriable && Vector2.Distance(tenant.P, at) < 260)
                            tenant.React(tenant.Type.IsAnimal ? null : "김치!", "heart", 2f);
                    desk.SpawnCoinAt(at - new Vector2(0, 54), reward);
                    break;
                case Kind.LanternStand:
                    label.Say("소원을 담아 등불을 띄웠어요", 0);
                    desk.Board?.Message("소원이 이루어지길", 8f);
                    for (int i = -1; i <= 1; i++)
                        desk.SpawnCoinAt(at + new Vector2(i * 32, -85), reward / 3);
                    break;
            }
            Debug.Log("[event] neighborhood visited: " + kind);
        }
    }
}
