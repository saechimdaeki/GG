using System.Collections.Generic;
using UnityEngine;

namespace CatChaos
{
    /// <summary>
    /// Immediate-mode HUD laid out on a 1280x720 canvas scaled to the window.
    /// Korean text uses the OS font (Apple SD Gothic Neo on mac).
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float W = 1280, Hgt = 720;
        static readonly Color Pink = new(1f, 0.62f, 0.72f), Cream = new(1f, 0.97f, 0.9f), Ink = new(0.22f, 0.16f, 0.2f);

        struct Popup { public string Text; public Vector3 At; public float T; public Color C; public int Size; }

        readonly List<Popup> _pops = new();
        Font _font;
        GUIStyle _s;
        Texture2D _white;
        string _toast;
        float _toastUntil, _comboPulse, _shownScore;
        Game G => Game.I;

        void Awake()
        {
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleSDGothicNeo-Bold", "Malgun Gothic", "Noto Sans CJK KR", "Arial Unicode MS" }, 32);
            _s = new GUIStyle { font = _font, richText = true, wordWrap = false };
            _white = Texture2D.whiteTexture;
        }

        public void Pop(string text, Vector3 at, Color c, int size = 24)
        {
            _pops.Add(new Popup { Text = text, At = at, T = Time.time, C = c, Size = size });
            if (_pops.Count > 40) _pops.RemoveAt(0);
        }

        public void Toast(string text, float seconds) { _toast = text; _toastUntil = Time.time + seconds; }
        public void ComboPulse() => _comboPulse = 1f;

        void OnGUI()
        {
            float k = Screen.height / Hgt;
            float w = Screen.width / k;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            _comboPulse = Mathf.MoveTowards(_comboPulse, 0, Time.unscaledDeltaTime * 3f);
            switch (G.S)
            {
                case Game.State.Intro: Intro(w); break;
                case Game.State.Play: Play(w, k); break;
                case Game.State.Paused: Play(w, k); Paused(w); break;
                case Game.State.Ending: Popups(k); Ending(w); break;
                case Game.State.Result: Result(w); break;
            }
        }

        // ------------------------------------------------------------------ screens
        void Intro(float w)
        {
            Fill(new Rect(0, 0, w, Hgt), new Color(0.15f, 0.08f, 0.12f, 0.35f));
            float bob = Mathf.Sin(Time.time * 2.2f) * 6f;
            Text(new Rect(0, 150 + bob, w, 130), "냥난리", 110, Pink, TextAnchor.MiddleCenter, 5);
            Text(new Rect(0, 285, w, 40), "주인이 퇴근하기 전에, 집을 최대한 어질러라!", 30, Cream, TextAnchor.MiddleCenter, 2);
            Panel(new Rect(w / 2 - 300, 360, 600, 196), new Color(1f, 1f, 1f, 0.9f));
            string[] keys =
            {
                "WASD  이동          Shift  우다다 달리기",
                "Space  점프 (공중에서 한 번 더)",
                "왼쪽 클릭  냥펀치          오른쪽 꾹  긁기 · 휴지/식탁보 물기",
                "Q  야옹          Esc  일시정지",
            };
            for (int i = 0; i < keys.Length; i++) Text(new Rect(w / 2 - 270, 378 + i * 42, 540, 40), keys[i], 23, Ink, TextAnchor.MiddleLeft);
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
            Text(new Rect(0, 590, w, 50), "클릭해서 시작", 34, new Color(1, 0.9f, 0.95f, blink), TextAnchor.MiddleCenter, 3);
        }

        void Play(float w, float k)
        {
            Popups(k);
            var sc = G.Score;
            _shownScore = Mathf.MoveTowards(_shownScore, sc.Total, Mathf.Max(30f, (sc.Total - _shownScore) * 8f) * Time.unscaledDeltaTime);

            // score
            Panel(new Rect(18, 18, 270, 104), new Color(1, 1, 1, 0.88f));
            Text(new Rect(36, 26, 230, 28), "난리 점수", 20, new Color(0.55f, 0.4f, 0.5f), TextAnchor.MiddleLeft);
            Text(new Rect(36, 52, 240, 52), Mathf.RoundToInt(_shownScore).ToString("N0"), 44, Ink, TextAnchor.MiddleLeft);
            if (sc.Combo > 1)
            {
                float s = 1 + _comboPulse * 0.35f;
                var r = new Rect(300, 30, 220, 70);
                var m = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(r.x * k, r.center.y * k));
                Text(r, $"x{sc.Combo} 콤보!", 40, Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.4f, 0.5f), sc.Combo / 5f), TextAnchor.MiddleLeft, 3);
                GUI.matrix = m;
                Fill(new Rect(302, 96, 160 * sc.ComboLeft(Time.time), 6), new Color(1f, 0.75f, 0.35f));
            }

            // clock
            float left = Mathf.Max(0, G.TimeLeft);
            bool late = left <= G.T.ownerWarnSeconds;
            float shake = late && left < 10 ? Mathf.Sin(Time.time * 40) * 2 : 0;
            var clock = new Rect(w / 2 - 150 + shake, 18, 300, 86);
            Panel(clock, late ? new Color(1f, 0.82f, 0.82f, 0.92f) : new Color(1, 1, 1, 0.88f));
            Text(new Rect(clock.x, 24, 300, 26), "주인 퇴근까지", 19, new Color(0.55f, 0.4f, 0.5f), TextAnchor.MiddleCenter);
            Text(new Rect(clock.x, 46, 300, 46), $"{(int)left / 60:00}:{(int)left % 60:00}", 40, late ? new Color(0.85f, 0.2f, 0.25f) : Ink, TextAnchor.MiddleCenter);
            Fill(new Rect(clock.x + 20, 94, 260, 5), new Color(0, 0, 0, 0.1f));
            Fill(new Rect(clock.x + 20, 94, 260 * left / G.T.roundSeconds, 5), late ? new Color(0.95f, 0.35f, 0.35f) : Pink);

            // favourite thing and fever
            float ey = 132;
            if (G.Target)
            {
                Panel(new Rect(18, ey, 270, 64), new Color(1f, 0.95f, 0.75f, 0.92f));
                Text(new Rect(34, ey + 6, 240, 26), "주인이 아끼는 물건 (!)", 18, new Color(0.65f, 0.45f, 0.2f), TextAnchor.MiddleLeft);
                Text(new Rect(34, ey + 30, 200, 30), G.Target.Label, 24, Ink, TextAnchor.MiddleLeft);
                Text(new Rect(200, ey + 30, 72, 30), $"{Mathf.CeilToInt(G.TargetLeft)}초", 22, new Color(0.85f, 0.35f, 0.25f), TextAnchor.MiddleRight);
                ey += 74;
            }
            if (G.FeverLeft > 0)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.04f;
                Panel(new Rect(18, ey, 270, 56), Color.HSVToRGB(Time.time * 0.3f % 1f, 0.35f, 1f));
                Text(new Rect(18, ey + 2, 270, 34), $"캣닢 피버 x{G.T.feverMult}!", Mathf.RoundToInt(26 * pulse), new Color(0.2f, 0.5f, 0.2f), TextAnchor.MiddleCenter);
                Fill(new Rect(38, ey + 40, 230 * G.FeverLeft / G.T.feverSeconds, 6), new Color(0.35f, 0.8f, 0.35f));
                ey += 66;
            }
            if (G.Rush)
            {
                Panel(new Rect(18, ey, 270, 44), new Color(1f, 0.85f, 0.6f, 0.92f));
                Text(new Rect(18, ey, 270, 44), $"막판 스퍼트  점수 x{G.T.rushMult:0.#}", 23, new Color(0.75f, 0.3f, 0.15f), TextAnchor.MiddleCenter);
            }

            // pet cam recording
            if (G.CamLeft > 0)
            {
                bool on = Mathf.Sin(Time.time * 6f) > 0;
                var r = new Rect(w / 2 - 230, 182, 460, 58);
                Panel(r, new Color(0.35f, 0.05f, 0.08f, 0.85f));
                Text(new Rect(r.x + 16, r.y + 2, 40, 54), on ? "●" : "", 30, new Color(1f, 0.25f, 0.25f), TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 46, r.y, 360, 34), $"REC  홈캠 촬영 중  {Mathf.CeilToInt(G.CamLeft)}초", 24, Color.white, TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 46, r.y + 30, 400, 26), "어지르면 감점 · Q 애교는 보너스", 17, new Color(1f, 0.8f, 0.8f), TextAnchor.MiddleLeft);
            }

            // goals
            float gx = w - 300, gy = 18, row = 30;
            Panel(new Rect(gx, gy, 282, 50 + G.Goals.Count * row), new Color(1, 1, 1, 0.88f));
            Text(new Rect(gx + 18, gy + 10, 250, 30), "오늘의 말썽", 22, new Color(0.75f, 0.35f, 0.5f), TextAnchor.MiddleLeft);
            for (int i = 0; i < G.Goals.Count; i++)
            {
                var g = G.Goals[i];
                float y = gy + 44 + i * row;
                Fill(new Rect(gx + 20, y + 8, 18, 18), g.Done ? new Color(0.45f, 0.8f, 0.5f) : new Color(0, 0, 0, 0.12f));
                if (g.Done) Text(new Rect(gx + 20, y + 4, 18, 22), "v", 18, Color.white, TextAnchor.MiddleCenter);
                var col = g.Done ? new Color(0.45f, 0.6f, 0.5f) : Ink;
                Text(new Rect(gx + 46, y + 2, 170, 30), g.Name, 19, col, TextAnchor.MiddleLeft);
                string prog = g.Done ? "완료" : g.Target > 1 ? $"{Mathf.Min(g.Value(), g.Target):0.#}/{g.Target:0}{g.Unit}" : "";
                Text(new Rect(gx + 200, y + 2, 66, 30), prog, 16, new Color(0.5f, 0.45f, 0.5f), TextAnchor.MiddleRight);
            }

            // toast
            if (Time.time < _toastUntil && !string.IsNullOrEmpty(_toast))
            {
                float a = Mathf.Clamp01((_toastUntil - Time.time) * 2f);
                var r = new Rect(w / 2 - 330, 122, 660, 52);
                Panel(r, new Color(0.25f, 0.15f, 0.22f, 0.8f * a));
                Text(r, _toast, 25, new Color(1, 1, 1, a), TextAnchor.MiddleCenter);
            }

            // hide hint
            if (left < G.T.hideHintSeconds)
            {
                float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 6);
                Text(new Rect(0, Hgt - 120, w, 40), "곧 주인이 온다! 방석이나 상자에 숨으면 시치미 보너스", 26, new Color(1f, 0.85f, 0.4f, blink), TextAnchor.MiddleCenter, 2);
            }

            // help
            if (G.T.roundSeconds - G.TimeLeft < 25f || UnityEngine.InputSystem.Keyboard.current?.hKey.isPressed == true)
                Text(new Rect(0, Hgt - 50, w, 36), "WASD 이동 · Shift 우다다 · Space 점프(2단) · 클릭 냥펀치 · 우클릭 꾹 긁기/물기 · Q 야옹 · 휠 줌", 19, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleCenter, 2);
            if (G.Cat.Scratching) Text(new Rect(0, Hgt - 160, w, 36), "벅벅벅벅", 28, Cream, TextAnchor.MiddleCenter, 2);
            else if (G.Cat.Holding == "휴지") Text(new Rect(0, Hgt - 160, w, 36), "휴지를 물었다! 우클릭 누른 채 달리면 쭉쭉 풀린다", 24, Cream, TextAnchor.MiddleCenter, 2);
            else if (G.Cat.Holding == "식탁보") Text(new Rect(0, Hgt - 160, w, 36), "식탁보를 물었다! 식탁 반대쪽으로 당겨라", 24, Cream, TextAnchor.MiddleCenter, 2);
            else if (G.Cat.CanGrab) Text(new Rect(0, Hgt - 160, w, 36), "우클릭 꾹: 물고 당기기", 22, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, 2);
        }

        void Paused(float w)
        {
            Fill(new Rect(0, 0, w, Hgt), new Color(0, 0, 0, 0.45f));
            Text(new Rect(0, 280, w, 80), "일시정지", 60, Cream, TextAnchor.MiddleCenter, 3);
            Text(new Rect(0, 370, w, 40), "클릭해서 계속", 28, Color.white, TextAnchor.MiddleCenter, 2);
        }

        void Ending(float w)
        {
            Fill(new Rect(0, 0, w, 70), Color.black);
            Fill(new Rect(0, Hgt - 70, w, 70), Color.black);
            if (string.IsNullOrEmpty(G.OwnerLine)) Text(new Rect(0, Hgt - 66, w, 60), "철컥… 철컥…", 30, Color.white, TextAnchor.MiddleCenter);
            else Text(new Rect(0, Hgt - 66, w, 60), "주인: " + G.OwnerLine, 32, Color.white, TextAnchor.MiddleCenter);
        }

        void Result(float w)
        {
            Fill(new Rect(0, 0, w, Hgt), new Color(0.12f, 0.06f, 0.1f, 0.55f));
            var r = new Rect(w / 2 - 330, 60, 660, 600);
            Panel(r, new Color(1f, 0.97f, 0.94f, 0.97f));
            string grade = Score.Grade(G.Score.Total, G.T.grades);
            var gc = grade switch { "S" => new Color(1f, 0.35f, 0.45f), "A" => new Color(1f, 0.55f, 0.3f), "B" => new Color(0.95f, 0.75f, 0.25f), "C" => new Color(0.4f, 0.7f, 0.9f), _ => new Color(0.55f, 0.55f, 0.6f) };
            Text(new Rect(r.x, 76, r.width, 34), "오늘의 난리 등급", 24, new Color(0.55f, 0.4f, 0.5f), TextAnchor.MiddleCenter);
            Text(new Rect(r.x, 104, r.width, 130), grade, 120, gc, TextAnchor.MiddleCenter, 4);
            Text(new Rect(r.x, 232, r.width, 50), G.Score.Total.ToString("N0") + "점", 42, Ink, TextAnchor.MiddleCenter);
            if (G.HideBonus > 0) Text(new Rect(r.x, 274, r.width, 30), $"숨어서 시치미 뚝  +{G.HideBonus:N0}", 21, new Color(0.85f, 0.45f, 0.6f), TextAnchor.MiddleCenter);
            var m = G.Mess;
            string[,] rows =
            {
                { "깨뜨린 물건", $"{m.Broken}개" },
                { "쓰러뜨리고 떨어뜨린 물건", $"{m.Tipped + m.Dropped}개" },
                { "풀어 버린 휴지", $"{m.Tissue:0.0}m" },
                { "가구 긁은 시간", $"{m.Scratch:0.0}초" },
                { "쏟은 화분", $"{m.Soil}개" },
                { "뒤집은 로봇청소기", $"{m.Vacuums}대" },
                { "로봇청소기가 치운 것", $"{m.Cleaned}개" },
                { "엎은 어항 · 잡은 나비", $"{m.Bowls}개 · {m.Butterflies}마리" },
                { "해치운 아끼는 물건", $"{m.Targets}개" },
                { "식탁보 · 빨래 · 들어간 상자", $"{m.Cloths} · {m.Laundry} · {m.Boxes}" },
                { "홈캠 애교 · 찍힌 말썽", $"{m.Cute}번 · {m.Caught}번" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = 304 + i * 21;
                Text(new Rect(r.x + 110, y, 300, 34), rows[i, 0], 20, new Color(0.4f, 0.32f, 0.38f), TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 350, y, 200, 34), rows[i, 1], 20, Ink, TextAnchor.MiddleRight);
            }
            Text(new Rect(r.x, 556, r.width, 40), "주인: " + G.OwnerLine, 26, new Color(0.75f, 0.3f, 0.4f), TextAnchor.MiddleCenter);
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
            Text(new Rect(r.x, 600, r.width, 40), "R  다시 하기", 26, new Color(Ink.r, Ink.g, Ink.b, blink), TextAnchor.MiddleCenter);
        }

        void Popups(float k)
        {
            var cam = Camera.main;
            if (!cam) return;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                float age = Time.time - p.T;
                if (age > 1.3f) { _pops.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(p.At);
                if (sp.z < 0) continue;
                float x = sp.x / k, y = (Screen.height - sp.y) / k - age * 45f;
                float pop = age < 0.12f ? Mathf.Lerp(0.6f, 1.15f, age / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1, (age - 0.12f) * 5));
                var c = p.C;
                c.a = Mathf.Clamp01((1.3f - age) * 2.5f);
                Text(new Rect(x - 200, y - 25, 400, 50), p.Text, Mathf.RoundToInt(p.Size * pop), c, TextAnchor.MiddleCenter, 2);
            }
        }

        // ------------------------------------------------------------------ drawing
        void Text(Rect r, string text, int size, Color c, TextAnchor anchor, int outline = 0)
        {
            _s.fontSize = size;
            _s.alignment = anchor;
            if (outline > 0)
            {
                _s.normal.textColor = new Color(0.2f, 0.1f, 0.15f, c.a * 0.75f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    GUI.Label(new Rect(r.x + Mathf.Cos(a) * outline, r.y + Mathf.Sin(a) * outline, r.width, r.height), text, _s);
                }
            }
            _s.normal.textColor = c;
            GUI.Label(r, text, _s);
        }

        void Panel(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, c.a * 0.18f);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 5, r.width, r.height), _white, ScaleMode.StretchToFill, true, 0, GUI.color, 0, 16);
            GUI.DrawTexture(r, _white, ScaleMode.StretchToFill, true, 0, c, 0, 16);
            GUI.color = old;
        }

        void Fill(Rect r, Color c) => GUI.DrawTexture(r, _white, ScaleMode.StretchToFill, true, 0, c, 0, 0);

    }
}
