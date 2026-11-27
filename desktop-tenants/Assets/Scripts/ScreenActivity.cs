using UnityEngine;

namespace DesktopTenants
{
    /// <summary>A short cursor game on one physical screen, independent of the town's buildings.</summary>
    public sealed class ScreenActivity
    {
        const int MaxTargets = 5;
        const float FinishTime = 3f;
        readonly Desk desk;
        readonly SpriteRenderer[] targets = new SpriteRenderer[MaxTargets];
        readonly Sprite bubbleSprite, starSprite;
        readonly Bubble notice;
        int floorId, nextScreen = 1, total, caught;
        float remaining, elapsed, duration;
        bool bubbles;

        public ScreenActivity(Desk desk)
        {
            this.desk = desk;
            var root = new GameObject("Screen activity").transform;
            root.SetParent(desk.transform, false);
            notice = Bubble.Create(root, Vector3.zero, 29);
            var pivot = new Vector2(0.5f, 0.5f);
            bubbleSprite = Props.Make(Props.ActivityBubble, desk.Tune.pixel * 1.5f, pivot);
            starSprite = Props.Make(Props.ActivityStar, desk.Tune.pixel * 1.5f, pivot);
            for (int i = 0; i < targets.Length; i++)
            {
                var obj = new GameObject("Activity target");
                obj.transform.SetParent(root, false);
                var target = obj.AddComponent<SpriteRenderer>();
                target.sortingOrder = 20;
                target.enabled = false;
                targets[i] = target;
            }
        }

        string Title => bubbles ? "비눗방울 소나기" : "별똥별 조각";

        public bool Start(bool popBubbles)
        {
            if (remaining > 0 || desk.DisplayCount == 0) return false;
            int screen = nextScreen % desk.DisplayCount;
            nextScreen = screen + 1;
            floorId = desk.DisplayBounds(screen).Floor.win;
            bubbles = popBubbles;
            total = bubbles ? MaxTargets : 3;
            caught = 0;
            elapsed = 0;
            duration = desk.Tune.screenActivityDuration;
            remaining = duration;
            for (int i = 0; i < total; i++)
            {
                targets[i].sprite = bubbles ? bubbleSprite : starSprite;
                targets[i].color = Color.white;
                targets[i].enabled = true;
            }
            ShowProgress();
            Debug.Log($"[event] {Title} started on display {screen + 1}");
            return true;
        }

        void ShowProgress() => notice.Say($"{Title} · 커서로 모으기 {caught}/{total}", 0);

        public void Tick(float dt)
        {
            if (remaining <= 0) return;
            remaining -= dt;
            if (remaining <= 0 || !desk.TryDisplayByFloor(floorId, out var display))
            {
                foreach (var target in targets) target.enabled = false;
                notice.Hide();
                remaining = 0;
                return;
            }

            var bounds = display.Bounds;
            notice.SetHome(Desk.W(new Vector2(bounds.center.x, bounds.yMin + 85)));
            if (caught == total) return;
            elapsed += dt;
            for (int i = 0; i < total; i++)
            {
                var target = targets[i];
                if (!target.enabled) continue;
                Vector2 at = Position(i, bounds, display.Floor.y);
                target.transform.position = Desk.W(at);
                if (!bubbles) target.color = new Color(1, 1, 1, 0.7f + Mathf.Sin(elapsed * 5 + i) * 0.3f);
                if (desk.DisplayAt(desk.Mouse) < 0 || (desk.Mouse - at).sqrMagnitude > 32f * 32f) continue;

                caught++;
                target.enabled = false;
                Bubble.Popup(bubbles ? "퐁!" : "반짝!", Desk.W(at - new Vector2(0, 25)));
                if (caught < total) { ShowProgress(); continue; }

                desk.SpawnCoinAt(at, desk.Tune.neighborhoodReward);
                notice.Say(bubbles ? "비눗방울을 모두 터뜨렸어요!" : "별똥별 조각을 모두 모았어요!", 0);
                remaining = FinishTime;
                Debug.Log($"[event] {Title} completed on floor {floorId}");
            }
        }

        Vector2 Position(int index, Rect bounds, float floorY)
        {
            float x = (index + 1f) / (total + 1);
            float phase = index * 2.4f;
            float y = bubbles
                ? Mathf.Lerp(0.72f - index * 0.09f, 0.16f, elapsed / duration)
                : 0.32f + index % 2 * 0.25f;
            float drift = bubbles ? Mathf.Sin(phase + elapsed * 1.2f) * bounds.width * 0.022f : 0;
            return new Vector2(bounds.xMin + x * bounds.width + drift, Mathf.Lerp(bounds.yMin, floorY, y));
        }
    }
}
